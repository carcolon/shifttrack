import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiFetch } from '../lib/api';
import AuthAnimatedShell from '../components/AuthAnimatedShell';
import { Button } from '../components/ui/Button';
import { ErrorPopup } from '../components/ui/ErrorPopup';
import { ConfirmModal } from '../components/Modals';
import type { ApiError, UserInfo, PtoRequest, PtoCoveragePreview } from '../types';
import { canReviewPtoForRole, isAdminRole } from '../lib/roles';

const prettify = (value: string) =>
  value
    .split('_')
    .map((v) => v.charAt(0).toUpperCase() + v.slice(1))
    .join(' ');

const toPastTense = (decision: 'approve' | 'deny') => (decision === 'approve' ? 'approved' : 'denied');

export default function PtoReviewPage({ user }: { user: UserInfo }) {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const requestId = params.get('requestId') ?? '';
  const [data, setData] = useState<PtoRequest | null>(null);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [approveWarning, setApproveWarning] = useState<string | null>(null);
  const [reviewComment, setReviewComment] = useState('');

  const canReview = canReviewPtoForRole(user.role);

  const load = async () => {
    if (!requestId) {
      setError('Request id is required.');
      return;
    }
    setLoading(true);
    setError('');
    try {
      const res = await apiFetch(`/pto/requests/${requestId}`);
      const json = (await res.json().catch(() => null)) as ApiError | PtoRequest | null;
      if (!res.ok) throw new Error((json as ApiError | null)?.message ?? 'Unable to load PTO request.');
      setData(json as PtoRequest);
    } catch (e: any) {
      setError(e.message ?? 'Unable to load PTO request.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [requestId]);

  const status = useMemo(() => (data?.status ?? '').toLowerCase(), [data?.status]);
  const reviewTitle = useMemo(
    () => ((data?.requestType ?? '').toLowerCase() === 'day_off' ? 'Day Off Request Review' : 'PTO Request Review'),
    [data?.requestType],
  );

  const loadCoveragePreview = async (targetRequestId: string) => {
    const res = await apiFetch(`/pto/requests/${targetRequestId}/coverage-preview`);
    const json = (await res.json().catch(() => null)) as ApiError | PtoCoveragePreview | null;
    if (!res.ok) throw new Error((json as ApiError | null)?.message ?? 'Unable to validate coverage.');
    return (json as PtoCoveragePreview) ?? { hasImpact: false, warnings: [] };
  };

  const submitDecision = async (decision: 'approve' | 'deny', skipCoverageCheck = false) => {
    if (!data || !canReview || status !== 'pending') return;
    if (!reviewComment.trim()) {
      setError('A comment is required to approve or deny this request.');
      return;
    }

    if (decision === 'approve' && isAdminRole(user.role) && !skipCoverageCheck) {
      try {
        const preview = await loadCoveragePreview(data.id);
        if (preview.warnings.length > 0) {
          setApproveWarning(preview.warnings.map((item) => item.message).join('\n'));
          return;
        }
      } catch (e: any) {
        setError(e.message ?? 'Unable to validate coverage.');
        return;
      }
    }

    setSubmitting(true);
    setMessage('');
    setError('');
    try {
      const res = await apiFetch(`/pto/requests/${data.id}/${decision}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ comments: reviewComment.trim() }),
      });
      const json = (await res.json().catch(() => null)) as ApiError | PtoRequest | null;
      if (!res.ok) throw new Error((json as ApiError | null)?.message ?? `Unable to ${decision} PTO request.`);
      setData(json as PtoRequest);
      setMessage(`PTO request ${toPastTense(decision)} successfully.`);
    } catch (e: any) {
      setError(e.message ?? `Unable to ${decision} PTO request.`);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthAnimatedShell>
      <div className="card pto-review-card">
        <div className="review-card-kicker">ShiftTrack Review</div>
        <h2>{reviewTitle}</h2>
        {loading && <p className="helper">Loading request...</p>}
        {!loading && <ErrorPopup message={error || null} onClose={() => setError('')} title="PTO review error" />}
        {approveWarning && (
          <ConfirmModal
            title="Coverage impact warning"
            description="This approval will affect coverage."
            message={approveWarning}
            onCancel={() => setApproveWarning(null)}
            onOk={() => {
              setApproveWarning(null);
              void submitDecision('approve', true);
            }}
          />
        )}
        {!loading && data && (
          <div className="modal-form review-card-body">
            <div className="review-details-grid">
              <div className="review-detail-item"><strong>Employee:</strong> {data.userDisplayName} ({data.userEmail})</div>
              <div className="review-detail-item"><strong>Request Type:</strong> {prettify(data.requestType)}</div>
              <div className="review-detail-item"><strong>Days:</strong> {data.numberOfDays}</div>
              <div className="review-detail-item"><strong>Start Date:</strong> {data.startDate}</div>
              <div className="review-detail-item"><strong>End Date:</strong> {data.endDate}</div>
              <div className="review-detail-item"><strong>Status:</strong> {prettify(data.status)}</div>
              <div className="review-detail-item review-detail-item-full"><strong>Comments:</strong> {data.comments?.trim() ? data.comments : 'N/A'}</div>
              {data.reviewComments && <div className="review-detail-item review-detail-item-full"><strong>Review Comment:</strong> {data.reviewComments}</div>}
              <div className="review-detail-item review-detail-item-full"><strong>Requested By:</strong> {data.requestedByName} ({data.requestedByEmail})</div>
            </div>
            {message && <div className="alert success">{message}</div>}
            {canReview && status === 'pending' && (
              <>
                <label className="field">
                  <span>Approval / rejection comment*</span>
                  <textarea value={reviewComment} onChange={(event) => setReviewComment(event.target.value)} rows={3} />
                </label>
                <div className="modal-actions">
                <Button variant="ghost" onClick={() => submitDecision('deny')} disabled={submitting}>
                  Deny
                </Button>
                <Button variant="primary" onClick={() => submitDecision('approve')} disabled={submitting}>
                  Approve
                </Button>
                </div>
              </>
            )}
          </div>
        )}
        <div className="actions">
          <Button variant="ghost" onClick={() => navigate('/app')}>
            Back to Calendar
          </Button>
        </div>
      </div>
    </AuthAnimatedShell>
  );
}
