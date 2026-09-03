import { useEffect, useMemo, useRef, useState } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Button } from '../components/ui/Button';
import { ErrorPopup } from '../components/ui/ErrorPopup';
import { StatusBadge } from '../components/ui/StatusBadge';
import { ToastStack, useToastStack } from '../components/ui/ToastStack';
import { ConfirmModal, ModalShell } from '../components/Modals';
import { apiFetch, getPreferredApiBase, getRealtimeAccessToken } from '../lib/api';
import { canReviewPtoForRole, isAdminRole, isEmployeeLikeRole, isManagerRole } from '../lib/roles';
import type { ApiError, PtoCoveragePreview, PtoRequest, RequestExportJob, SwapRequest, UserInfo } from '../types';

type RequestStatusTab = 'pending' | 'approved' | 'denied' | 'closed';
type RequestFamily = 'pto' | 'swap' | 'dayoff';
type ReviewDecision = 'approve' | 'deny';
type ReviewModalState = {
  family: RequestFamily;
  requestId: string;
  decision: ReviewDecision;
  subject: string;
} | null;

const prettify = (value: string) =>
  value
    .split('_')
    .map((v) => v.charAt(0).toUpperCase() + v.slice(1))
    .join(' ');

const formatSwapDate = (value: string) => {
  const parsed = new Date(`${value}T00:00:00`);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
};

const describeEntry = (label: string, type: string) => (type === 'dayOff' ? 'Day Off' : label);

const formatLocalDateTime = (isoDate?: string | null) => {
  if (!isoDate) return 'N/A';
  const parsed = new Date(isoDate);
  if (Number.isNaN(parsed.getTime())) return isoDate;
  return parsed.toLocaleString('en-US', {
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    second: '2-digit',
    hour12: true,
    timeZoneName: 'short',
  });
};

const readDownloadFileName = (header: string | null) => {
  if (!header) return '';
  const utf8Match = header.match(/filename\*=UTF-8''([^;]+)/i);
  if (utf8Match?.[1]) return decodeURIComponent(utf8Match[1].replace(/"/g, ''));
  const asciiMatch = header.match(/filename="?([^";]+)"?/i);
  return asciiMatch?.[1] ?? '';
};

export function RequestsPage({ user }: { user: UserInfo }) {
  const [activeStatus, setActiveStatus] = useState<RequestStatusTab>('pending');
  const canReviewPto = canReviewPtoForRole(user.role);
  const canViewOwnDayOff = isEmployeeLikeRole(user.role);
  const canViewPto = canReviewPto || isEmployeeLikeRole(user.role);
  const canViewDayOff = canReviewPto || canViewOwnDayOff;
  const [activeFamily, setActiveFamily] = useState<RequestFamily>(canViewPto ? 'pto' : canViewDayOff ? 'dayoff' : 'swap');
  const [ptoItems, setPtoItems] = useState<PtoRequest[]>([]);
  const [swapItems, setSwapItems] = useState<SwapRequest[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionLoadingId, setActionLoadingId] = useState<string | null>(null);
  const [approveWarning, setApproveWarning] = useState<{ requestId: string; message: string; comment: string } | null>(null);
  const [reviewModal, setReviewModal] = useState<ReviewModalState>(null);
  const [reviewCommentDraft, setReviewCommentDraft] = useState('');
  const [exportingRequests, setExportingRequests] = useState(false);
  const [exportRealtimeReady, setExportRealtimeReady] = useState(false);
  const { toasts, pushToast, dismissToast } = useToastStack();
  const exportTimeoutRef = useRef<ReturnType<typeof window.setTimeout> | null>(null);

  const loadRequests = async (family: RequestFamily, status: RequestStatusTab) => {
    setLoading(true);
    setError(null);
    try {
      if (family === 'swap') {
        const requestedStatus = status === 'closed' ? 'denied' : status;
        const params = new URLSearchParams({ status: requestedStatus, take: '300' });
        const res = await apiFetch(`/swap/requests?${params.toString()}`);
        const json = (await res.json().catch(() => null)) as ApiError | SwapRequest[] | null;
        if (!res.ok) throw new Error((json as ApiError | null)?.message ?? 'Unable to load swap requests.');
        setSwapItems((json as SwapRequest[]) ?? []);
      } else if (status === 'closed') {
        const deniedParams = new URLSearchParams({ status: 'denied', take: '300' });
        const canceledParams = new URLSearchParams({ status: 'canceled', take: '300' });
        const [deniedRes, canceledRes] = await Promise.all([
          apiFetch(`/pto/requests?${deniedParams.toString()}`),
          apiFetch(`/pto/requests?${canceledParams.toString()}`),
        ]);
        const deniedJson = (await deniedRes.json().catch(() => null)) as ApiError | PtoRequest[] | null;
        const canceledJson = (await canceledRes.json().catch(() => null)) as ApiError | PtoRequest[] | null;
        if (!deniedRes.ok) throw new Error((deniedJson as ApiError | null)?.message ?? 'Unable to load denied requests.');
        if (!canceledRes.ok) throw new Error((canceledJson as ApiError | null)?.message ?? 'Unable to load canceled requests.');

        const merged = [...((deniedJson as PtoRequest[]) ?? []), ...((canceledJson as PtoRequest[]) ?? [])]
          .sort((a, b) => {
            const left = new Date(b.createdAtUtc).getTime();
            const right = new Date(a.createdAtUtc).getTime();
            return left - right;
          });
        setPtoItems(merged);
      } else {
        const params = new URLSearchParams({ status, take: '300' });
        const res = await apiFetch(`/pto/requests?${params.toString()}`);
        const json = (await res.json().catch(() => null)) as ApiError | PtoRequest[] | null;
        if (!res.ok) throw new Error((json as ApiError | null)?.message ?? `Unable to load ${family} requests.`);
        setPtoItems((json as PtoRequest[]) ?? []);
      }
    } catch (e: any) {
      setError(e.message ?? `Unable to load ${family} requests.`);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRequests(activeFamily, activeStatus);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeFamily, activeStatus]);

  const performPtoAction = async (requestId: string, action: 'approve' | 'deny' | 'cancel', reviewComment?: string) => {
    const comment = reviewComment?.trim();
    if (action !== 'cancel' && !comment) {
      setError('A comment is required to approve or deny a request.');
      return;
    }
    setActionLoadingId(requestId);
    setError(null);
    try {
      const res = await apiFetch(`/pto/requests/${requestId}/${action}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: action === 'cancel' ? undefined : JSON.stringify({ comments: comment }),
      });
      const json = (await res.json().catch(() => null)) as ApiError | PtoRequest | null;
      if (!res.ok) throw new Error((json as ApiError | null)?.message ?? `Unable to ${action} request.`);
      await loadRequests(activeFamily === 'swap' ? 'pto' : activeFamily, activeStatus);
    } catch (e: any) {
      setError(e.message ?? `Unable to ${action} request.`);
    } finally {
      setActionLoadingId(null);
    }
  };

  const loadCoveragePreview = async (requestId: string) => {
    const res = await apiFetch(`/pto/requests/${requestId}/coverage-preview`);
    const json = (await res.json().catch(() => null)) as ApiError | PtoCoveragePreview | null;
    if (!res.ok) throw new Error((json as ApiError | null)?.message ?? 'Unable to validate coverage.');
    return (json as PtoCoveragePreview) ?? { hasImpact: false, warnings: [] };
  };

  const handlePtoApprove = async (requestId: string, reviewComment: string, skipCoverageCheck = false) => {
    if (isAdminRole(user.role) && !skipCoverageCheck) {
      try {
        const preview = await loadCoveragePreview(requestId);
        if (preview.warnings.length > 0) {
          setApproveWarning({
            requestId,
            message: preview.warnings.map((item) => item.message).join('\n'),
            comment: reviewComment,
          });
          return;
        }
      } catch (e: any) {
        setError(e.message ?? 'Unable to validate coverage.');
        return;
      }
    }

    await performPtoAction(requestId, 'approve', reviewComment);
  };

  const performSwapAction = async (requestId: string, action: 'approve' | 'deny', reviewComment: string) => {
    const comment = reviewComment.trim();
    if (!comment) {
      setError('A comment is required to approve or deny a request.');
      return;
    }
    setActionLoadingId(requestId);
    setError(null);
    try {
      const res = await apiFetch(`/swap/requests/${requestId}/${action}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ comments: comment }),
      });
      const json = (await res.json().catch(() => null)) as ApiError | SwapRequest | null;
      if (!res.ok) throw new Error((json as ApiError | null)?.message ?? `Unable to ${action} swap request.`);
      await loadRequests('swap', activeStatus);
    } catch (e: any) {
      setError(e.message ?? `Unable to ${action} swap request.`);
    } finally {
      setActionLoadingId(null);
    }
  };

  const performSwapCancel = async (requestId: string) => {
    setActionLoadingId(requestId);
    setError(null);
    try {
      const res = await apiFetch(`/swap/requests/${requestId}/cancel`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
      });
      const json = (await res.json().catch(() => null)) as ApiError | SwapRequest | null;
      if (!res.ok) throw new Error((json as ApiError | null)?.message ?? 'Unable to cancel swap request.');
      await loadRequests('swap', activeStatus);
    } catch (e: any) {
      setError(e.message ?? 'Unable to cancel swap request.');
    } finally {
      setActionLoadingId(null);
    }
  };

  const openReviewModal = (family: RequestFamily, requestId: string, decision: ReviewDecision, subject: string) => {
    setError(null);
    setReviewModal({ family, requestId, decision, subject });
    setReviewCommentDraft('');
  };

  const closeReviewModal = () => {
    if (actionLoadingId) return;
    setReviewModal(null);
    setReviewCommentDraft('');
  };

  const submitReviewModal = async () => {
    if (!reviewModal) return;
    const comment = reviewCommentDraft.trim();
    if (!comment) {
      setError('A comment is required to approve or deny a request.');
      return;
    }

    if (reviewModal.family === 'swap') {
      await performSwapAction(reviewModal.requestId, reviewModal.decision, comment);
    } else if (reviewModal.decision === 'approve') {
      await handlePtoApprove(reviewModal.requestId, comment);
    } else {
      await performPtoAction(reviewModal.requestId, 'deny', comment);
    }

    setReviewModal(null);
    setReviewCommentDraft('');
  };

  const downloadExport = async (job: RequestExportJob) => {
    const res = await apiFetch(job.downloadUrl || `/requests/exports/${job.id}/download`);
    const payload = res.ok ? null : ((await res.json().catch(() => null)) as ApiError | null);
    if (!res.ok) throw new Error(payload?.message ?? 'Unable to download requests export.');

    const blob = await res.blob();
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = readDownloadFileName(res.headers.get('Content-Disposition')) || job.fileName || 'shifttrack-requests-export.xlsx';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    window.URL.revokeObjectURL(url);
  };

  useEffect(() => {
    if (!canReviewPto) return;

    setExportRealtimeReady(false);
    const hubUrl = `${getPreferredApiBase().replace(/\/+$/, '')}/hubs/schedule`;
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        withCredentials: true,
        accessTokenFactory: () => getRealtimeAccessToken(),
      })
      .withAutomaticReconnect([0, 1500, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    const clearExportTimeout = () => {
      if (exportTimeoutRef.current) {
        window.clearTimeout(exportTimeoutRef.current);
        exportTimeoutRef.current = null;
      }
    };

    connection.onreconnecting(() => setExportRealtimeReady(false));
    connection.onreconnected(async () => {
      await connection.invoke('JoinRequestExports').then(() => setExportRealtimeReady(true)).catch(() => setExportRealtimeReady(false));
    });
    connection.onclose(() => setExportRealtimeReady(false));

    connection.on('requests.export.status', (job: RequestExportJob) => {
      clearExportTimeout();
      setExportingRequests(false);

      if (job.status === 'completed') {
        pushToast({
          tone: 'success',
          title: 'Export ready',
          message: job.fileName || 'Requests export is ready to download.',
          actionLabel: 'Download',
          onAction: async () => {
            try {
              await downloadExport(job);
            } catch (e: any) {
              setError(e.message ?? 'Unable to download requests export.');
            }
          },
          autoDismissMs: null,
        });
        return;
      }

      if (job.status === 'failed') {
        setError(job.errorMessage || 'Requests export failed.');
      }
    });

    void connection
      .start()
      .then(() => connection.invoke('JoinRequestExports'))
      .then(() => setExportRealtimeReady(true))
      .catch(() => setExportRealtimeReady(false));

    return () => {
      clearExportTimeout();
      setExportRealtimeReady(false);
      connection.off('requests.export.status');
      void connection.invoke('LeaveRequestExports').catch(() => undefined).finally(() => {
        void connection.stop();
      });
    };
  }, [canReviewPto, pushToast]);

  const handleExportRequests = async () => {
    if (exportingRequests) return;
    if (!exportRealtimeReady) {
      setError('Export notifications are still connecting. Try again in a moment.');
      return;
    }
    setExportingRequests(true);
    setError(null);
    if (exportTimeoutRef.current) {
      window.clearTimeout(exportTimeoutRef.current);
      exportTimeoutRef.current = null;
    }

    try {
      const startRes = await apiFetch('/requests/exports', { method: 'POST' });
      const startJob = (await startRes.json().catch(() => null)) as ApiError | RequestExportJob | null;
      if (!startRes.ok) throw new Error((startJob as ApiError | null)?.message ?? 'Unable to start requests export.');

      const jobId = (startJob as RequestExportJob | null)?.id;
      if (!jobId) throw new Error('Requests export did not return a job id.');
      if (exportTimeoutRef.current) window.clearTimeout(exportTimeoutRef.current);
      exportTimeoutRef.current = window.setTimeout(() => {
        setExportingRequests(false);
        setError('Requests export is taking longer than expected. Try again in a moment.');
      }, 10 * 60 * 1000);
    } catch (e: any) {
      setError(e.message ?? 'Unable to export requests.');
      setExportingRequests(false);
    }
  };

  const ptoOnlyItems = useMemo(() => ptoItems.filter((item) => item.requestType !== 'day_off'), [ptoItems]);
  const dayOffItems = useMemo(() => ptoItems.filter((item) => item.requestType === 'day_off'), [ptoItems]);
  const ptoVisibleItems = activeFamily === 'dayoff' ? dayOffItems : ptoOnlyItems;
  const items = activeFamily === 'swap' ? swapItems : ptoVisibleItems;
  const titleByStatus = useMemo(
    () => ({
      pending: activeFamily === 'swap' ? 'Pending Swap Requests' : activeFamily === 'dayoff' ? 'Pending Day Off Requests' : 'Pending PTO Requests',
      approved: activeFamily === 'swap' ? 'Approved Swap Requests' : activeFamily === 'dayoff' ? 'Approved Day Off Requests' : 'Approved PTO Requests',
      denied: activeFamily === 'swap' ? 'Denied Swap Requests' : activeFamily === 'dayoff' ? 'Denied Day Off Requests' : 'Denied PTO Requests',
      closed: activeFamily === 'swap' ? 'Denied Swap Requests' : activeFamily === 'dayoff' ? 'Closed Day Off Requests (Denied / Canceled)' : 'Closed PTO Requests (Denied / Canceled)',
    }),
    [activeFamily],
  );

  return (
    <div className="card">
      <div className="card-header">
        <div className="member-scope-wrap">
          <h2>Requests</h2>
          <div className="member-scope-toggle">
            {canViewPto && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                active={activeFamily === 'pto'}
                onClick={() => setActiveFamily('pto')}
              >
                PTO
              </Button>
            )}
            <Button
              type="button"
              variant="ghost"
              size="sm"
              active={activeFamily === 'swap'}
              onClick={() => setActiveFamily('swap')}
            >
              Swaps
            </Button>
            {canViewDayOff && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                active={activeFamily === 'dayoff'}
                onClick={() => setActiveFamily('dayoff')}
              >
                Days Off
              </Button>
            )}
          </div>
        </div>
        {canReviewPto && (
          <div className="requests-export-wrap">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="requests-export-btn"
              disabled={exportingRequests || !exportRealtimeReady}
              onClick={handleExportRequests}
            >
              {exportingRequests ? 'Preparing...' : exportRealtimeReady ? 'Export Excel' : 'Connecting...'}
            </Button>
          </div>
        )}
        <div className="member-scope-toggle">
          <Button type="button" variant="ghost" size="sm" active={activeStatus === 'pending'} onClick={() => setActiveStatus('pending')}>
            Pending
          </Button>
          <Button type="button" variant="ghost" size="sm" active={activeStatus === 'approved'} onClick={() => setActiveStatus('approved')}>
            Approved
          </Button>
          {activeFamily === 'swap' ? (
            <Button type="button" variant="ghost" size="sm" active={activeStatus === 'denied'} onClick={() => setActiveStatus('denied')}>
              Denied
            </Button>
          ) : (
            <Button type="button" variant="ghost" size="sm" active={activeStatus === 'closed'} onClick={() => setActiveStatus('closed')}>
              Denied / Canceled
            </Button>
          )}
        </div>
      </div>

      <p className="helper">{titleByStatus[activeStatus]}</p>
      {loading && <p className="helper">Loading requests...</p>}
      <ErrorPopup message={error} onClose={() => setError(null)} title="Request error" />
      <ToastStack toasts={toasts} onDismiss={dismissToast} />
      {approveWarning && (
        <ConfirmModal
          title="Coverage impact warning"
          description="This approval will affect coverage."
          message={approveWarning.message}
          onCancel={() => setApproveWarning(null)}
          onOk={() => {
            const { requestId, comment } = approveWarning;
            setApproveWarning(null);
            void handlePtoApprove(requestId, comment, true);
          }}
        />
      )}
      {reviewModal && (
        <ModalShell className="review-decision-modal" ariaLabel="Review request decision" onBackdropClick={closeReviewModal}>
          <div className="review-decision-head">
            <span className={`review-decision-kicker ${reviewModal.decision}`}>
              {reviewModal.decision === 'approve' ? 'Approval comment' : 'Rejection comment'}
            </span>
            <h2>{reviewModal.decision === 'approve' ? 'Approve request' : 'Deny request'}</h2>
            <p>
              Add the comment that will be saved with the request and sent by email.
            </p>
          </div>
          <div className="review-decision-subject">
            <span>Request</span>
            <strong>{reviewModal.subject}</strong>
          </div>
          <label className="review-comment-field">
            <span>Comment required</span>
            <textarea
              value={reviewCommentDraft}
              onChange={(event) => setReviewCommentDraft(event.target.value)}
              placeholder={reviewModal.decision === 'approve' ? 'Explain why this request is approved...' : 'Explain why this request is denied...'}
              rows={5}
              autoFocus
            />
          </label>
          <div className="modal-actions">
            <Button variant="ghost" onClick={closeReviewModal} disabled={Boolean(actionLoadingId)}>
              Cancel
            </Button>
            <Button
              variant={reviewModal.decision === 'approve' ? 'primary' : 'dangerGhost'}
              onClick={submitReviewModal}
              disabled={Boolean(actionLoadingId)}
            >
              {reviewModal.decision === 'approve' ? 'Approve with comment' : 'Deny with comment'}
            </Button>
          </div>
        </ModalShell>
      )}

      {!loading && !error && items.length === 0 && <p className="helper">No requests found.</p>}

      {!loading && !error && (activeFamily === 'pto' || activeFamily === 'dayoff') && ptoVisibleItems.length > 0 && (
        <div className="requests-list">
          {ptoVisibleItems.map((request) => (
            <div key={request.id} className="request-card">
              <div className="request-card-head">
                <strong>{request.userDisplayName}</strong>
                <StatusBadge status={request.status} />
              </div>
              <div><strong>Email:</strong> {request.userEmail}</div>
              <div><strong>Type:</strong> {prettify(request.requestType)}</div>
              <div><strong>Days:</strong> {request.numberOfDays}</div>
              <div><strong>Start:</strong> {request.startDate}</div>
              <div><strong>End:</strong> {request.endDate}</div>
              <div><strong>Comments:</strong> {request.comments?.trim() ? request.comments : 'N/A'}</div>
              {request.reviewComments && <div><strong>Review Comment:</strong> {request.reviewComments}</div>}
              <div><strong>Requested By:</strong> {request.requestedByName} ({request.requestedByEmail})</div>
              {request.reviewedByEmail && (
                <div><strong>Reviewed By:</strong> {request.reviewedByName || request.reviewedByEmail} ({request.reviewedByEmail})</div>
              )}

              <div className="request-actions">
                {activeStatus === 'pending' && (
                  <>
                    {canReviewPto ? (
                      <>
                        <Button
                          variant="ghost"
                          disabled={actionLoadingId === request.id}
                          onClick={() => openReviewModal(activeFamily, request.id, 'deny', `${request.userDisplayName} · ${prettify(request.requestType)}`)}
                        >
                          Deny
                        </Button>
                        <Button
                          variant="primary"
                          disabled={actionLoadingId === request.id}
                          onClick={() => openReviewModal(activeFamily, request.id, 'approve', `${request.userDisplayName} · ${prettify(request.requestType)}`)}
                        >
                          Approve
                        </Button>
                      </>
                    ) : (
                      activeFamily === 'dayoff' && (
                        <Button
                          variant="dangerGhost"
                          disabled={actionLoadingId === request.id}
                          onClick={() => performPtoAction(request.id, 'cancel')}
                        >
                          Cancel Request
                        </Button>
                      )
                    )}
                  </>
                )}

                {canReviewPto && activeStatus === 'approved' && (
                  <Button variant="dangerGhost" disabled={actionLoadingId === request.id} onClick={() => performPtoAction(request.id, 'cancel')}>
                    Cancel Request
                  </Button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {!loading && !error && activeFamily === 'swap' && swapItems.length > 0 && (
        <div className="requests-list">
          {swapItems.map((request) => {
            const canReview = activeStatus === 'pending' && canReviewPto;
            const isRequester = request.requestedByEmail.trim().toLowerCase() === user.email.trim().toLowerCase();
            const canManagerCancel =
              isAdminRole(user.role) ||
              (isManagerRole(user.role) && request.requestedByRole !== 2 && request.targetUserRole !== 2);
            const futureApproved =
              request.pairs.every((pair) => new Date(`${pair.requesterCurrent.date}T00:00:00`).getTime() > new Date().setHours(0, 0, 0, 0)) &&
              request.pairs.every((pair) => new Date(`${pair.targetCurrent.date}T00:00:00`).getTime() > new Date().setHours(0, 0, 0, 0));
            const canCancelPending = activeStatus === 'pending' && (isRequester || canManagerCancel);
            const canCancelApproved = activeStatus === 'approved' && canManagerCancel && futureApproved;
            return (
              <div key={request.id} className="request-card">
                <div className="request-card-head">
                  <strong>{request.requestedByDisplayName}</strong>
                  <StatusBadge status={request.status} />
                </div>
                <div><strong>Requester:</strong> {request.requestedByDisplayName} ({request.requestedByEmail})</div>
                <div><strong>Swap With:</strong> {request.targetUserDisplayName} ({request.targetUserEmail})</div>
                <div><strong>Type:</strong> {prettify(request.requestType)}</div>
                <div><strong>Requested At:</strong> {formatLocalDateTime(request.createdAtUtc)}</div>
                <div><strong>Observations:</strong> {request.comments?.trim() ? request.comments : 'N/A'}</div>
                {request.reviewComments && <div><strong>Review Comment:</strong> {request.reviewComments}</div>}
                {request.weeklyHours?.map((week) => (
                  <div key={`${request.id}-${week.weekStart}`} className={week.requesterHours > week.limitHours || week.targetHours > week.limitHours ? 'alert warning' : ''}>
                    <strong>Week of {week.weekStart}:</strong> {request.requestedByDisplayName} will work {week.requesterHours} hours; {request.targetUserDisplayName} will work {week.targetHours} hours.
                    {(week.requesterHours > week.limitHours || week.targetHours > week.limitHours) && ` Weekly limit: ${week.limitHours} hours.`}
                  </div>
                ))}
                <div>
                  <strong>Swap Details:</strong>
                  <div className="request-swap-visuals">
                    {request.pairs.map((pair, index) => (
                      <section key={`${request.id}-pair-${index}`} className="request-swap-visual-card">
                        <div className="request-swap-visual-head">
                          <span className="request-swap-visual-kicker">Schedule Change</span>
                          <strong>{formatSwapDate(pair.requesterCurrent.date)}</strong>
                        </div>
                        <div className="request-swap-visual-grid">
                          <div className="request-swap-person-card">
                            <span className="request-swap-person-name">{request.requestedByDisplayName}</span>
                            <div className="request-swap-flow">
                              <span className="request-swap-state before">
                                {describeEntry(pair.requesterCurrent.label, pair.requesterCurrent.type)}
                              </span>
                              <span className="request-swap-arrow">→</span>
                              <span className="request-swap-state after">
                                {describeEntry(pair.requesterResult.label, pair.requesterResult.type)}
                              </span>
                            </div>
                          </div>
                          <div className="request-swap-person-card">
                            <span className="request-swap-person-name">{request.targetUserDisplayName}</span>
                            <div className="request-swap-flow">
                              <span className="request-swap-state before">
                                {describeEntry(pair.targetCurrent.label, pair.targetCurrent.type)}
                              </span>
                              <span className="request-swap-arrow">→</span>
                              <span className="request-swap-state after">
                                {describeEntry(pair.targetResult.label, pair.targetResult.type)}
                              </span>
                            </div>
                          </div>
                        </div>
                      </section>
                    ))}
                  </div>
                </div>
                {request.reviewedByEmail && (
                  <>
                    <div><strong>Reviewed By:</strong> {request.reviewedByName || request.reviewedByEmail} ({request.reviewedByEmail})</div>
                    <div><strong>Reviewed At:</strong> {formatLocalDateTime(request.reviewedAtUtc)}</div>
                  </>
                )}

                <div className="request-actions">
                  {canReview && (
                    <>
                      <Button
                        variant="ghost"
                        disabled={actionLoadingId === request.id}
                        onClick={() => openReviewModal('swap', request.id, 'deny', `${request.requestedByDisplayName} ↔ ${request.targetUserDisplayName}`)}
                      >
                        Deny
                      </Button>
                      <Button
                        variant="primary"
                        disabled={actionLoadingId === request.id}
                        onClick={() => openReviewModal('swap', request.id, 'approve', `${request.requestedByDisplayName} ↔ ${request.targetUserDisplayName}`)}
                      >
                        Approve
                      </Button>
                    </>
                  )}
                  {canCancelPending && (
                    <Button variant="dangerGhost" disabled={actionLoadingId === request.id} onClick={() => performSwapCancel(request.id)}>
                      Cancel Request
                    </Button>
                  )}
                  {canCancelApproved && (
                    <Button variant="dangerGhost" disabled={actionLoadingId === request.id} onClick={() => performSwapCancel(request.id)}>
                      Cancel Approved Swap
                    </Button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default RequestsPage;
