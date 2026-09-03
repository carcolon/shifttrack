IF OBJECT_ID(N'dbo.SwapRequests', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_SwapRequests_Status'
          AND parent_object_id = OBJECT_ID(N'dbo.SwapRequests')
    )
    BEGIN
        ALTER TABLE dbo.SwapRequests
        DROP CONSTRAINT CK_SwapRequests_Status;
    END;

    ALTER TABLE dbo.SwapRequests
    ADD CONSTRAINT CK_SwapRequests_Status
        CHECK (Status IN ('pending', 'approved', 'denied', 'canceled'));
END;
