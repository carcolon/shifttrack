using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Application;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal interface IUserWorkflowService
{
    Task<IResult> CreateUserAsync(HttpContext httpContext, CreateUserRequest request);
    Task<IResult> ListUsersAsync(HttpContext httpContext, bool inactiveOnly);
    Task<IResult> ListSystemHiddenUsersAsync(HttpContext httpContext);
    Task<IResult> UpdateUserAsync(HttpContext httpContext, Guid id, UpdateUserRequest request);
    Task<IResult> BulkUploadUsersAsync(HttpContext httpContext, IFormFile file);
    Task<IResult> DeleteUserAsync(HttpContext httpContext, Guid id);
    Task<IResult> ReactivateUserAsync(HttpContext httpContext, Guid id);
    Task<IResult> PurgeUserAsync(HttpContext httpContext, Guid id);
}

internal sealed class UserWorkflowService : IUserWorkflowService
{
    private const string CorporateEmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    private const int MaxDisplayNameLength = 200;
    private const int MaxEmailLength = 320;
    private const int MaxLocationLength = 50;
    private const int MaxCompanyLength = 200;
    private const int MaxOperationLength = 120;
    private const int MaxShiftTimeLength = 50;
    private readonly IAuthService _auth;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _hasher;
    private readonly IUserRepository _users;
    private readonly IHubContext<ScheduleHub> _hub;
    private readonly StartupOptions _options;

    public UserWorkflowService(
        IAuthService auth,
        IEmailService emailService,
        IPasswordHasher hasher,
        IUserRepository users,
        IHubContext<ScheduleHub> hub,
        StartupOptions options)
    {
        _auth = auth;
        _emailService = emailService;
        _hasher = hasher;
        _users = users;
        _hub = hub;
        _options = options;
    }

    public async Task<IResult> CreateUserAsync(HttpContext httpContext, CreateUserRequest request)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanManageUsers(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        var callerRole = callerContext.Role;
        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (RoleHelpers.IsManager(callerRole) && !RoleHelpers.CanManagerManageRole(request.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        if (!CompanyScopeHelpers.CanAssignCompanies(callerUser, request.Company, request.Companies))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        if (request.IsSystemHidden && !callerUser.IsSystemHidden)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        if (request.IsSystemHidden && !RoleHelpers.IsAdmin(request.Role))
        {
            return Results.BadRequest(new ErrorResponse("Super admins must have the Admin role."));
        }
        if (!RoleHelpers.IsKnownRole(request.Role))
        {
            return Results.BadRequest(new ErrorResponse("Role is invalid."));
        }

        var missingField = request.GetMissingField();
        if (missingField is not null)
        {
            return Results.BadRequest(new ErrorResponse($"Please complete the required field: {missingField}."));
        }

        var lengthValidation = ValidateUserFieldLengths(request.FirstName, request.LastName, request.Email, request.Location, request.Company, request.Operation);
        if (lengthValidation is not null)
        {
            return Results.BadRequest(new ErrorResponse(lengthValidation));
        }

        if (!Regex.IsMatch(request.Email.Trim(), CorporateEmailPattern))
        {
            return Results.BadRequest(new ErrorResponse("Credentials for the entered email are not valid. Please check and try again."));
        }

        var requestSchedulePeriods = request.SchedulePeriods?.ToArray() ?? Array.Empty<SchedulePeriodRequest>();
        var hasSchedule = requestSchedulePeriods.Length > 0;
        var scheduleValidation = request.IsSystemHidden && !hasSchedule
            ? null
            : SchedulePeriodHelpers.ValidateSchedulePeriods(requestSchedulePeriods);
        if (scheduleValidation is not null)
        {
            return Results.BadRequest(new ErrorResponse(scheduleValidation));
        }
        var scheduleLengthValidation = ValidateScheduleFieldLengths(requestSchedulePeriods);
        if (scheduleLengthValidation is not null)
        {
            return Results.BadRequest(new ErrorResponse(scheduleLengthValidation));
        }

        var displayName = $"{request.FirstName} {request.LastName}".Trim();
        var periodsForCreate = SchedulePeriodHelpers.BuildSchedulePeriods(Guid.NewGuid(), requestSchedulePeriods);
        var legacyShiftTime = SchedulePeriodHelpers.BuildLegacyShiftTime(periodsForCreate);
        var legacyScheduleJson = SchedulePeriodHelpers.BuildLegacyScheduleBlocksJson(periodsForCreate);
        var result = await _auth.CreateUserAsync(
            request.Email.Trim(),
            displayName,
            request.Role,
            request.Password.Trim(),
            request.Location,
            request.Company,
            request.Companies,
            request.Operation,
            legacyShiftTime,
            legacyScheduleJson);
        if (!result.Success)
        {
            if (result.Message?.Contains("already associated") == true)
            {
                return Results.BadRequest(new ErrorResponse(result.Message));
            }

            return Results.BadRequest(new ErrorResponse(result.Message ?? "Unable to create user."));
        }

        var token = await _auth.GenerateResetTokenAsync(request.Email.Trim(), TimeSpan.FromMinutes(60));
        var resetCode = token is null ? string.Empty : CreateResetCode(request.Email.Trim(), token, TimeSpan.FromMinutes(60));
        var resetLink = string.IsNullOrWhiteSpace(resetCode) ? string.Empty : ApiHelpers.BuildResetLink(_options.FrontendBaseUrl, resetCode);
        await _emailService.SendWelcomeEmailAsync(request.Email.Trim(), displayName, request.Password.Trim(), resetLink);

        var actor = ApiHelpers.ExtractActor(httpContext, callerRole);
        var createdUser = await _users.GetByEmailAsync(request.Email.Trim());
        if (createdUser is not null)
        {
            if (request.IsSystemHidden)
            {
                await _users.SetSystemHiddenAsync(createdUser.Id, true);
                createdUser = await _users.GetByIdAsync(createdUser.Id) ?? createdUser;
            }
            var createdPeriods = !hasSchedule
                ? Array.Empty<UserSchedulePeriod>()
                : requestSchedulePeriods.Select(period => new UserSchedulePeriod
                {
                    Id = Guid.NewGuid(),
                    UserId = createdUser.Id,
                    EffectiveFrom = DateTime.ParseExact(period.EffectiveFrom, "yyyy-MM-dd", null).Date,
                    EffectiveTo = string.IsNullOrWhiteSpace(period.EffectiveTo) ? null : DateTime.ParseExact(period.EffectiveTo, "yyyy-MM-dd", null).Date,
                    ShiftTime = period.ShiftTime.Trim(),
                    BlocksJson = JsonSerializer.Serialize(period.ScheduleBlocks ?? Array.Empty<ScheduleBlockRequest>()),
                    CreatedAtUtc = DateTime.UtcNow
                }).ToArray();
            await _users.ReplaceUserSchedulePeriodsAsync(createdUser.Id, createdPeriods, legacyShiftTime, legacyScheduleJson);
        }
        await ApiHelpers.PublishScheduleEventAsync(
            _users,
            _hub,
            "created",
            createdUser?.Id,
            request.Email.Trim(),
            actor,
            JsonSerializer.Serialize(new
            {
                request.Role,
                request.Location,
                request.Company,
                Companies = BuildResponseCompanies(createdUser),
                request.Operation,
                request.IsSystemHidden,
                SchedulePeriods = request.SchedulePeriods
            }));

        return Results.Ok(new { Message = "User created successfully." });
    }

    public async Task<IResult> ListSystemHiddenUsersAsync(HttpContext httpContext)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.IsAdmin(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null || !callerUser.IsSystemHidden)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var list = (await _users.GetSystemHiddenAsync())
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.Role,
                user.Operation,
                user.Location,
                user.Company,
                Companies = BuildResponseCompanies(user),
                user.ShiftTime,
                user.IsSystemHidden,
                SchedulePeriods = SchedulePeriodHelpers.ToSchedulePeriodDtos(user.SchedulePeriods)
            });

        return Results.Ok(list);
    }

    public async Task<IResult> ListUsersAsync(HttpContext httpContext, bool inactiveOnly)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) ||
            !RoleHelpers.CanManageUsers(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var data = inactiveOnly ? await _users.GetInactiveAsync() : await _users.GetAllAsync();
        var list = data
            .Where(user => CompanyScopeHelpers.IsInCallerCompanyScope(callerUser, user))
            .Select(user => new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.Operation,
            user.Location,
            user.Company,
            Companies = BuildResponseCompanies(user),
            user.ShiftTime,
            SchedulePeriods = SchedulePeriodHelpers.ToSchedulePeriodDtos(user.SchedulePeriods)
        });
        return Results.Ok(list);
    }

    public async Task<IResult> UpdateUserAsync(HttpContext httpContext, Guid id, UpdateUserRequest request)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanManageUsers(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        var callerRole = callerContext.Role;
        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var existing = await _users.GetByIdAsync(id);
        if (existing is null || !existing.IsActive) return Results.NotFound();
        if (!CompanyScopeHelpers.IsInCallerCompanyScope(callerUser, existing) ||
            !CompanyScopeHelpers.CanAssignCompanies(callerUser, request.Company, request.Companies))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        if (!RoleHelpers.IsKnownRole(request.Role)) return Results.BadRequest(new ErrorResponse("Role is invalid."));
        var requestedSystemHidden = request.IsSystemHidden ?? existing.IsSystemHidden;
        if (request.IsSystemHidden.HasValue &&
            request.IsSystemHidden.Value != existing.IsSystemHidden &&
            !callerUser.IsSystemHidden)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        if (requestedSystemHidden && !RoleHelpers.IsAdmin(request.Role))
        {
            return Results.BadRequest(new ErrorResponse("Super admins must have the Admin role."));
        }
        if (existing.IsSystemHidden && !requestedSystemHidden)
        {
            var validation = await ValidateSystemHiddenRemovalAsync(callerUser, callerContext, existing);
            if (validation is not null) return validation;
        }
        if (RoleHelpers.IsManager(callerRole) &&
            (!RoleHelpers.CanManagerManageRole(existing.Role) || !RoleHelpers.CanManagerManageRole(request.Role)))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var missing = request.GetMissingField(requestedSystemHidden);
        if (missing is not null) return Results.BadRequest(new ErrorResponse($"Please complete the required field: {missing}."));
        var lengthValidation = ValidateUserFieldLengths(request.FirstName, request.LastName, existing.Email, request.Location, request.Company, request.Operation);
        if (lengthValidation is not null) return Results.BadRequest(new ErrorResponse(lengthValidation));
        var requestSchedulePeriods = request.SchedulePeriods?.ToArray() ?? Array.Empty<SchedulePeriodRequest>();
        var hasSchedule = requestSchedulePeriods.Length > 0;
        var scheduleValidation = requestedSystemHidden && !hasSchedule
            ? null
            : SchedulePeriodHelpers.ValidateSchedulePeriods(requestSchedulePeriods);
        if (scheduleValidation is not null) return Results.BadRequest(new ErrorResponse(scheduleValidation));
        var scheduleLengthValidation = ValidateScheduleFieldLengths(requestSchedulePeriods);
        if (scheduleLengthValidation is not null) return Results.BadRequest(new ErrorResponse(scheduleLengthValidation));

        var displayName = $"{request.FirstName} {request.LastName}".Trim();
        var periods = SchedulePeriodHelpers.BuildSchedulePeriods(id, requestSchedulePeriods);
        var scheduleJson = SchedulePeriodHelpers.BuildLegacyScheduleBlocksJson(periods);
        var shiftTime = SchedulePeriodHelpers.BuildLegacyShiftTime(periods);
        var companyScope = CompanyScopeHelpers.BuildCompanyScopeJson(request.Companies, request.Company);
        var rows = await _users.UpdateUserAsync(id, displayName, request.Role, request.Location, request.Company, companyScope, request.Operation, shiftTime, scheduleJson);
        if (rows <= 0)
        {
            return Results.BadRequest(new ErrorResponse("Unable to update user."));
        }
        if (request.IsSystemHidden.HasValue && request.IsSystemHidden.Value != existing.IsSystemHidden)
        {
            await _users.SetSystemHiddenAsync(id, request.IsSystemHidden.Value);
        }
        await _users.ReplaceUserSchedulePeriodsAsync(id, periods, shiftTime, scheduleJson);

        var actor = ApiHelpers.ExtractActor(httpContext, callerRole);
        await ApiHelpers.PublishScheduleEventAsync(
            _users,
            _hub,
            "updated",
            id,
            existing.Email,
            actor,
            JsonSerializer.Serialize(new
            {
                request.Role,
                request.Location,
                request.Company,
                Companies = CompanyScopeHelpers.ResolveCompanies(companyScope, request.Company),
                request.Operation,
                IsSystemHidden = requestedSystemHidden,
                SchedulePeriods = request.SchedulePeriods
            }));

        return Results.Ok(new { Message = "User updated successfully." });
    }

    public async Task<IResult> BulkUploadUsersAsync(HttpContext httpContext, IFormFile file)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanManageUsers(callerContext.Role))
        {
            return Results.Json(new BulkUserUploadResponse
            {
                Message = "Bulk upload is only available for managers and admins.",
                Errors = [new BulkUserUploadError(0, "Authorization", string.Empty, "Caller must be Manager or Admin.")]
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null)
        {
            return Results.Json(new BulkUserUploadResponse
            {
                Message = "Bulk upload failed authorization.",
                Errors = [new BulkUserUploadError(0, "Authorization", callerContext.Email, "Caller user was not found or is inactive.")]
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        var (rows, parseErrors) = await BulkUserUploadHelpers.ReadRowsAsync(file);
        if (parseErrors.Count > 0)
        {
            return Results.BadRequest(new BulkUserUploadResponse
            {
                Message = "Bulk upload validation failed. No users were changed.",
                Errors = parseErrors.ToArray()
            });
        }

        var grouped = rows
            .GroupBy(row => row.Email, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var validationErrors = new List<BulkUserUploadError>();
        var allUsers = (await _users.GetAllAsync())
            .Concat(await _users.GetInactiveAsync())
            .Concat(await _users.GetSystemHiddenAsync())
            .ToArray();
        var existingByEmail = allUsers
            .GroupBy(user => user.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var operations = (await _users.GetCompanyOperationsAsync(null, includeInactive: false)).ToArray();
        var upserts = new List<BulkUserUpsert>();
        var created = 0;
        var updated = 0;

        foreach (var group in grouped)
        {
            var orderedRows = group.OrderBy(row => row.RowNumber).ThenBy(row => row.PeriodNumber).ThenBy(row => row.BlockNumber).ToArray();
            var first = orderedRows[0];
            ValidateConsistentUserFields(orderedRows, validationErrors);
            ValidatePeriodFields(orderedRows, validationErrors);

            if (RoleHelpers.IsManager(callerContext.Role) && !RoleHelpers.CanManagerManageRole(first.Role))
            {
                validationErrors.Add(new BulkUserUploadError(first.RowNumber, "Role*", first.Email, $"Manager callers cannot create or update users with role '{RoleLabel(first.Role)}'."));
            }

            if (!CompanyScopeHelpers.CanAssignCompanies(callerUser, first.PrimaryCompany, first.Companies))
            {
                validationErrors.Add(new BulkUserUploadError(first.RowNumber, "Companies*", first.Email, $"Company scope violation. Caller scope is [{string.Join(", ", CompanyScopeHelpers.ResolveCompanies(callerUser))}], but row requests [{string.Join(", ", first.Companies)}] with primary company '{first.PrimaryCompany}'."));
            }

            var operationError = ValidateOperation(first, operations);
            if (operationError is not null)
            {
                validationErrors.Add(operationError);
            }

            var requestedPeriods = BulkUserUploadHelpers.BuildRequestedPeriods(orderedRows);
            var periodValidation = SchedulePeriodHelpers.ValidateSchedulePeriods(requestedPeriods);
            if (periodValidation is not null)
            {
                validationErrors.Add(new BulkUserUploadError(first.RowNumber, "Schedule", first.Email, periodValidation));
            }

            var scheduleLengthValidation = ValidateScheduleFieldLengths(requestedPeriods);
            if (scheduleLengthValidation is not null)
            {
                validationErrors.Add(new BulkUserUploadError(first.RowNumber, "Shift Time*", first.Email, scheduleLengthValidation));
            }

            var lengthValidation = ValidateUserFieldLengths(first.FirstName, first.LastName, first.Email, first.Location, first.PrimaryCompany, first.Operation);
            if (lengthValidation is not null)
            {
                validationErrors.Add(new BulkUserUploadError(first.RowNumber, "User", first.Email, lengthValidation));
            }

            if (!Regex.IsMatch(first.Email.Trim(), CorporateEmailPattern))
            {
                validationErrors.Add(new BulkUserUploadError(first.RowNumber, "Email*", first.Email, "Email format is invalid."));
            }

            existingByEmail.TryGetValue(first.Email, out var existing);
            if (existing is not null)
            {
                if (!existing.IsActive)
                {
                    validationErrors.Add(new BulkUserUploadError(first.RowNumber, "Email*", first.Email, "Existing user is inactive. Reactivate the user before bulk updating."));
                }
                if (existing.IsSystemHidden)
                {
                    validationErrors.Add(new BulkUserUploadError(first.RowNumber, "Email*", first.Email, "Bulk upload cannot create or update system-hidden users."));
                }
                if (!CompanyScopeHelpers.IsInCallerCompanyScope(callerUser, existing))
                {
                    validationErrors.Add(new BulkUserUploadError(first.RowNumber, "Email*", first.Email, $"Existing user is outside caller company scope. User companies are [{string.Join(", ", CompanyScopeHelpers.ResolveCompanies(existing))}]."));
                }
                if (RoleHelpers.IsManager(callerContext.Role) &&
                    (!RoleHelpers.CanManagerManageRole(existing.Role) || !RoleHelpers.CanManagerManageRole(first.Role)))
                {
                    validationErrors.Add(new BulkUserUploadError(first.RowNumber, "Role*", first.Email, $"Manager callers cannot update existing role '{RoleLabel(existing.Role)}' to '{RoleLabel(first.Role)}'."));
                }
            }

            if (validationErrors.Any(error => error.Email.Equals(first.Email, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var displayName = $"{first.FirstName} {first.LastName}".Trim();
            var incomingPeriods = SchedulePeriodHelpers.BuildSchedulePeriods(existing?.Id ?? Guid.NewGuid(), requestedPeriods);
            var userId = existing?.Id ?? incomingPeriods[0].UserId;
            incomingPeriods = incomingPeriods.Select(period => new UserSchedulePeriod
            {
                Id = period.Id,
                UserId = userId,
                EffectiveFrom = period.EffectiveFrom,
                EffectiveTo = period.EffectiveTo,
                ShiftTime = period.ShiftTime,
                BlocksJson = period.BlocksJson,
                IsRepeating = period.IsRepeating,
                CreatedAtUtc = period.CreatedAtUtc
            }).ToArray();
            var finalPeriods = existing is null
                ? incomingPeriods
                : BulkUserUploadHelpers.MergeSchedulePeriods(userId, existing.SchedulePeriods, incomingPeriods);
            var legacyShiftTime = SchedulePeriodHelpers.BuildLegacyShiftTime(finalPeriods);
            var legacyScheduleJson = SchedulePeriodHelpers.BuildLegacyScheduleBlocksJson(finalPeriods);
            var companyScope = CompanyScopeHelpers.BuildCompanyScopeJson(first.Companies, first.PrimaryCompany);

            var user = new User
            {
                Id = userId,
                TenantId = existing?.TenantId ?? callerUser.TenantId,
                ObjectId = existing?.ObjectId ?? Guid.NewGuid(),
                Email = first.Email,
                DisplayName = displayName,
                Role = first.Role,
                IsActive = true,
                IsSystemHidden = existing?.IsSystemHidden ?? false,
                PasswordHash = existing?.PasswordHash ?? _hasher.Hash(BulkUserUploadHelpers.GenerateTempPassword()),
                MustChangePassword = existing?.MustChangePassword ?? true,
                CreatedAtUtc = existing?.CreatedAtUtc ?? DateTime.UtcNow,
                Location = first.Location,
                Company = first.PrimaryCompany,
                CompanyScope = companyScope,
                Operation = first.Operation,
                ShiftTime = legacyShiftTime,
                ScheduleBlocks = legacyScheduleJson
            };

            upserts.Add(new BulkUserUpsert(user, finalPeriods, legacyShiftTime, legacyScheduleJson, existing is null));
            if (existing is null) created++;
            else updated++;
        }

        if (validationErrors.Count > 0)
        {
            return Results.BadRequest(new BulkUserUploadResponse
            {
                Message = "Bulk upload validation failed. No users were changed.",
                RowsProcessed = rows.Count,
                Errors = validationErrors.OrderBy(error => error.Row).ToArray()
            });
        }

        try
        {
            await _users.BulkUpsertUsersAsync(upserts);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new BulkUserUploadResponse
            {
                Message = "Bulk upload failed while saving. No users were changed.",
                RowsProcessed = rows.Count,
                Errors = [new BulkUserUploadError(0, "Database", string.Empty, ex.Message)]
            });
        }

        var actor = ApiHelpers.ExtractActor(httpContext, callerContext.Role);
        try
        {
            foreach (var item in upserts)
            {
                await ApiHelpers.PublishScheduleEventAsync(
                    _users,
                    _hub,
                    item.IsNewUser ? "bulk-created" : "bulk-updated",
                    item.User.Id,
                    item.User.Email,
                    actor,
                    JsonSerializer.Serialize(new
                    {
                        item.User.Role,
                        item.User.Location,
                        item.User.Company,
                        Companies = CompanyScopeHelpers.ResolveCompanies(item.User),
                        item.User.Operation,
                        SchedulePeriods = SchedulePeriodHelpers.ToSchedulePeriodDtos(item.SchedulePeriods)
                    }));
            }
        }
        catch
        {
            // Realtime notifications are best-effort after the transactional import has succeeded.
        }

        return Results.Ok(new BulkUserUploadResponse
        {
            Message = $"Bulk upload completed. Created {created} user(s), updated {updated} user(s).",
            Created = created,
            Updated = updated,
            RowsProcessed = rows.Count
        });
    }

    public Task<IResult> DeleteUserAsync(HttpContext httpContext, Guid id) => ChangeUserActivationAsync(httpContext, id, "inactivated");

    public Task<IResult> ReactivateUserAsync(HttpContext httpContext, Guid id) => ChangeUserActivationAsync(httpContext, id, "reactivated");

    public async Task<IResult> PurgeUserAsync(HttpContext httpContext, Guid id)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanManageUsers(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var confirmation = httpContext.Request.Headers["X-Purge-Confirm"].ToString();
        if (!string.Equals(confirmation, "PURGE", StringComparison.Ordinal))
        {
            return Results.BadRequest(new ErrorResponse("Explicit purge confirmation is required."));
        }

        var existing = await _users.GetByIdAsync(id);
        if (existing is null)
        {
            return Results.NotFound();
        }

        if (existing.IsActive)
        {
            return Results.BadRequest(new ErrorResponse("Only inactive users can be permanently deleted."));
        }

        if (existing.IsSystemHidden)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!CanManageTargetUser(callerUser, callerContext.Role, existing))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var rows = await _users.HardDeleteAsync(id);
        if (rows <= 0)
        {
            return Results.BadRequest(new ErrorResponse("Unable to permanently delete user."));
        }

        var actor = ApiHelpers.ExtractActor(httpContext, callerContext.Role);
        await ApiHelpers.PublishScheduleEventAsync(
            _users,
            _hub,
            "purged",
            id,
            existing.Email,
            actor,
            JsonSerializer.Serialize(new
            {
                ExistingRole = existing.Role,
                existing.Location,
                existing.Company,
                existing.Operation,
                existing.ShiftTime
            }));

        return Results.Ok(new { Message = "User permanently deleted." });
    }

    private async Task<IResult> ChangeUserActivationAsync(HttpContext httpContext, Guid id, string action)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanManageUsers(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        var callerRole = callerContext.Role;
        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var existing = await _users.GetByIdAsync(id);
        if (existing is null || (action == "inactivated" && !existing.IsActive)) return Results.NotFound();
        if (existing.IsSystemHidden && action == "inactivated")
        {
            var validation = await ValidateSystemHiddenRemovalAsync(callerUser, callerContext, existing);
            if (validation is not null) return validation;
        }
        if (!CanManageTargetUser(callerUser, callerRole, existing))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var rows = action switch
        {
            "inactivated" => await _users.SoftDeleteAsync(id),
            "reactivated" => await _users.ReactivateAsync(id),
            _ => 0
        };
        if (rows <= 0)
        {
            return Results.BadRequest(new ErrorResponse(action switch
            {
                "inactivated" => "Unable to delete user.",
                "reactivated" => "Unable to reactivate user.",
                _ => "Unable to permanently delete user."
            }));
        }

        var actor = ApiHelpers.ExtractActor(httpContext, callerRole);
        await ApiHelpers.PublishScheduleEventAsync(
            _users,
            _hub,
            action,
            id,
            existing.Email,
            actor,
            JsonSerializer.Serialize(new
            {
                ExistingRole = existing.Role,
                existing.Location,
                existing.Company,
                existing.Operation,
                existing.ShiftTime
            }));

        return Results.Ok(new
        {
            Message = action switch
            {
                "inactivated" => "User set as inactive.",
                "reactivated" => "User reactivated.",
                _ => "User permanently deleted."
            }
        });
    }

    private async Task<User?> ResolveCallerUserAsync(CallerContext callerContext)
    {
        if (callerContext.UserId.HasValue)
        {
            var byId = await _users.GetByIdAsync(callerContext.UserId.Value);
            if (byId is not null && byId.IsActive)
            {
                return byId;
            }
        }

        return string.IsNullOrWhiteSpace(callerContext.Email)
            ? null
            : await _users.GetByEmailAsync(callerContext.Email);
    }

    private static void ValidateConsistentUserFields(IReadOnlyCollection<BulkUploadRow> rows, ICollection<BulkUserUploadError> errors)
    {
        var first = rows.OrderBy(row => row.RowNumber).First();
        foreach (var row in rows.Skip(1))
        {
            if (!string.Equals(row.FirstName, first.FirstName, StringComparison.Ordinal) ||
                !string.Equals(row.LastName, first.LastName, StringComparison.Ordinal) ||
                row.Role != first.Role ||
                !string.Equals(row.Location, first.Location, StringComparison.Ordinal) ||
                !row.Companies.SequenceEqual(first.Companies, StringComparer.OrdinalIgnoreCase) ||
                !string.Equals(row.PrimaryCompany, first.PrimaryCompany, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(row.Operation, first.Operation, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new BulkUserUploadError(row.RowNumber, "User fields", row.Email, "Rows for the same Email must repeat the same user fields: First Name, Last Name, Role, Location, Companies, Primary Company, and Operation."));
            }
        }
    }

    private static void ValidatePeriodFields(IReadOnlyCollection<BulkUploadRow> rows, ICollection<BulkUserUploadError> errors)
    {
        foreach (var periodGroup in rows.GroupBy(row => row.PeriodNumber))
        {
            var first = periodGroup.OrderBy(row => row.RowNumber).First();
            var blockNumbers = new HashSet<int>();
            foreach (var row in periodGroup)
            {
                if (!string.Equals(row.EffectiveFrom, first.EffectiveFrom, StringComparison.Ordinal) ||
                    !string.Equals(row.EffectiveTo ?? string.Empty, first.EffectiveTo ?? string.Empty, StringComparison.Ordinal) ||
                    !string.Equals(row.ShiftTime, first.ShiftTime, StringComparison.OrdinalIgnoreCase) ||
                    row.IsRepeating != first.IsRepeating)
                {
                    errors.Add(new BulkUserUploadError(row.RowNumber, "Period Number*", row.Email, $"All rows with Period Number {row.PeriodNumber} for this Email must use the same Effective From, Effective To, Shift Time, and Is Repeating values."));
                }

                if (!blockNumbers.Add(row.BlockNumber))
                {
                    errors.Add(new BulkUserUploadError(row.RowNumber, "Block Number*", row.Email, $"Block Number {row.BlockNumber} is duplicated inside Period Number {row.PeriodNumber}."));
                }
            }
        }
    }

    private static BulkUserUploadError? ValidateOperation(BulkUploadRow row, IReadOnlyCollection<CompanyOperationItem> operations)
    {
        var hasCatalogEntries = operations.Any(item => string.Equals(item.CompanyName, row.PrimaryCompany, StringComparison.OrdinalIgnoreCase));
        if (!hasCatalogEntries) return null;

        var exists = operations.Any(item =>
            item.IsActive &&
            string.Equals(item.CompanyName, row.PrimaryCompany, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Name, row.Operation, StringComparison.OrdinalIgnoreCase));
        return exists
            ? null
            : new BulkUserUploadError(row.RowNumber, "Operation*", row.Email, $"Operation '{row.Operation}' is not active for company '{row.PrimaryCompany}'.");
    }

    private static string RoleLabel(int role) => role switch
    {
        RoleHelpers.Employee => "Employee",
        RoleHelpers.Manager => "Manager",
        RoleHelpers.Admin => "Admin",
        RoleHelpers.TeamLeader => "Team Leader",
        _ => $"Unknown ({role})"
    };

    private static string[] BuildResponseCompanies(User? user) =>
        user is null ? Array.Empty<string>() : CompanyScopeHelpers.ResolveCompanies(user);

    private static string? ValidateUserFieldLengths(string firstName, string lastName, string email, string location, string company, string operation)
    {
        var displayName = $"{firstName} {lastName}".Trim();
        if (displayName.Length > MaxDisplayNameLength) return $"Display name must be {MaxDisplayNameLength} characters or fewer.";
        if (email.Trim().Length > MaxEmailLength) return $"Email must be {MaxEmailLength} characters or fewer.";
        if (location.Trim().Length > MaxLocationLength) return $"Location must be {MaxLocationLength} characters or fewer.";
        if (company.Trim().Length > MaxCompanyLength) return $"Company must be {MaxCompanyLength} characters or fewer.";
        if (operation.Trim().Length > MaxOperationLength) return $"Operation must be {MaxOperationLength} characters or fewer.";
        return null;
    }

    private static string? ValidateScheduleFieldLengths(IEnumerable<SchedulePeriodRequest> periods)
    {
        var invalidPeriod = periods.FirstOrDefault(period => period.ShiftTime.Trim().Length > MaxShiftTimeLength);
        return invalidPeriod is null ? null : $"Shift time must be {MaxShiftTimeLength} characters or fewer.";
    }

    private static bool CanManageTargetUser(User callerUser, int callerRole, User targetUser)
    {
        if (callerUser.IsSystemHidden) return true;
        if (!CompanyScopeHelpers.IsInCallerCompanyScope(callerUser, targetUser)) return false;
        if (RoleHelpers.IsAdmin(callerRole)) return !RoleHelpers.IsAdmin(targetUser.Role);
        if (RoleHelpers.IsManager(callerRole)) return RoleHelpers.IsEmployeeLike(targetUser.Role);
        return false;
    }

    private async Task<IResult?> ValidateSystemHiddenRemovalAsync(User callerUser, CallerContext callerContext, User targetUser)
    {
        if (!callerUser.IsSystemHidden)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (IsSameUser(callerUser, callerContext, targetUser))
        {
            return Results.BadRequest(new ErrorResponse("A super admin cannot remove or deactivate themselves."));
        }

        if (targetUser.IsActive && await _users.CountActiveSystemHiddenAsync() <= 1)
        {
            return Results.BadRequest(new ErrorResponse("At least one active super admin is required."));
        }

        return null;
    }

    private static bool IsSameUser(User callerUser, CallerContext callerContext, User targetUser)
    {
        if (callerUser.Id == targetUser.Id) return true;
        if (callerContext.UserId.HasValue && callerContext.UserId.Value == targetUser.Id) return true;
        return string.Equals(callerUser.Email, targetUser.Email, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(callerContext.Email, targetUser.Email, StringComparison.OrdinalIgnoreCase);
    }
}
