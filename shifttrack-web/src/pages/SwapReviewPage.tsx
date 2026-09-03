import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiFetch } from '../lib/api';
import AuthAnimatedShell from '../components/AuthAnimatedShell';
import { Button } from '../components/ui/Button';
import { ErrorPopup } from '../components/ui/ErrorPopup';
import type { ApiError, SwapRequest, UserInfo } from '../types';
import { canReviewPtoForRole } from '../lib/roles';

const prettify = (value: string) =>
  value
    .split('_')
    .map((v) => v.charAt(0).toUpperCase() + v.slice(1))
    .join(' ');

const toPastTense = (decision: 'approve' | 'deny') => (decision === 'approve' ? 'approved' : 'denied');

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

export default function SwapReviewPage({ user }: { user: UserInfo }) {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const requestId = params.get('requestId') ?? '';
  const [data, setData] = useState<SwapRequest | null>(null);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [reviewComment, setReviewComment] = useState('');

  const canReview = useMemo(
    () => !!data && canReviewPtoForRole(user.role) && data.status.toLowerCase() === 'pending',
    [data, user.role]
  );

  const reviewPairs = useMemo(
    () =>
      (data?.pairs ?? []).map((pair, index) => ({
        id: `${data?.id ?? 'swap'}-${index}`,
        date: pair.requesterCurrent.date,
        requesterBefore: describeEntry(pair.requesterCurrent.label, pair.requesterCurrent.type),
        requesterAfter: describeEntry(pair.requesterResult.label, pair.requesterResult.type),
        targetBefore: describeEntry(pair.targetCurrent.label, pair.targetCurrent.type),
        targetAfter: describeEntry(pair.targetResult.label, pair.targetResult.type),
      })),
    [data],
  );

  useEffect(() => {
    const load = async () => {
      if (!requestId) {
        setError('Request id is required.');
        return;
      }
      setLoading(true);
      setError('');
      try {
        const res = await apiFetch(`/swap/requests/${requestId}`);
        const json = (await res.json().catch(() => null)) as ApiError | SwapRequest | null;
        if (!res.ok) throw new Error((json as ApiError | null)?.message ?? 'Unable to load swap request.');
        setData(json as SwapRequest);
      } catch (e: any) {
        setError(e.message ?? 'Unable to load swap request.');
      } finally {
        setLoading(false);
      }
    };

    void load();
  }, [requestId]);

  const submitDecision = async (decision: 'approve' | 'deny') => {
    if (!data || !canReview) return;
    if (!reviewComment.trim()) {
      setError('A comment is required to approve or deny this request.');
      return;
    }

    setSubmitting(true);
    setMessage('');
    setError('');
    try {
      const res = await apiFetch(`/swap/requests/${data.id}/${decision}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ comments: reviewComment.trim() }),
      });
      const json = (await res.json().catch(() => null)) as ApiError | SwapRequest | null;
      if (!res.ok) throw new Error((json as ApiError | null)?.message ?? `Unable to ${decision} swap request.`);
      setData(json as SwapRequest);
      setMessage(`Swap request ${toPastTense(decision)} successfully.`);
    } catch (e: any) {
      setError(e.message ?? `Unable to ${decision} swap request.`);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthAnimatedShell>
      <div className="card pto-review-card">
        <div className="review-card-kicker">ShiftTrack Review</div>
        <h2>Swap Request Review</h2>
        {loading && <p className="helper">Loading request...</p>}
        {!loading && <ErrorPopup message={error || null} onClose={() => setError('')} title="Swap review error" />}
        {!loading && data && (
          <div className="modal-form review-card-body">
            <div className="review-details-grid">
              <div className="review-detail-item"><strong>Requester:</strong> {data.requestedByDisplayName} ({data.requestedByEmail})</div>
              <div className="review-detail-item"><strong>Swap With:</strong> {data.targetUserDisplayName} ({data.targetUserEmail})</div>
              <div className="review-detail-item"><strong>Type:</strong> {prettify(data.requestType)}</div>
              <div className="review-detail-item"><strong>Status:</strong> {prettify(data.status)}</div>
              <div className="review-detail-item"><strong>Requested At:</strong> {formatLocalDateTime(data.createdAtUtc)}</div>
              <div className="review-detail-item"><strong>Reviewed At:</strong> {formatLocalDateTime(data.reviewedAtUtc)}</div>
              <div className="review-detail-item review-detail-item-full"><strong>Observations:</strong> {data.comments?.trim() ? data.comments : 'N/A'}</div>
              {data.reviewComments && <div className="review-detail-item review-detail-item-full"><strong>Review Comment:</strong> {data.reviewComments}</div>}
            </div>
            {data.weeklyHours?.map((week) => (
              <div key={week.weekStart} className={week.requesterHours > week.limitHours || week.targetHours > week.limitHours ? 'alert warning' : 'alert'}>
                <strong>Week of {week.weekStart}:</strong> If approved, {data.requestedByDisplayName} will work {week.requesterHours} hours and {data.targetUserDisplayName} will work {week.targetHours} hours.
                {(week.requesterHours > week.limitHours || week.targetHours > week.limitHours) && ` This exceeds the ${week.limitHours}-hour weekly limit.`}
              </div>
            ))}
            <div className="swap-review-summary">
              <div className="swap-review-summary-card">
                <span className="swap-review-summary-label">What the requester gets</span>
                <strong>{formatSwapDate(data.targetDates[0] ?? data.swapDate)}</strong>
                <span>{data.targetUserDisplayName}'s day off moves to {data.requestedByDisplayName}</span>
              </div>
              <div className="swap-review-summary-card">
                <span className="swap-review-summary-label">What the coworker gets</span>
                <strong>{formatSwapDate(data.requestedDates[0] ?? data.swapDate)}</strong>
                <span>{data.requestedByDisplayName}'s day off moves to {data.targetUserDisplayName}</span>
              </div>
            </div>
            <div className="swap-review-pairs">
              {reviewPairs.map((pair) => (
                <section key={pair.id} className="swap-review-pair-card">
                  <div className="swap-review-pair-head">
                    <span className="swap-review-pair-kicker">Schedule Change</span>
                    <strong>{formatSwapDate(pair.date)}</strong>
                  </div>
                  <div className="swap-review-people-grid">
                    <div className="swap-review-person-card">
                      <span className="swap-review-person-name">{data.requestedByDisplayName}</span>
                      <div className="swap-review-flow">
                        <span className="swap-review-state before">{pair.requesterBefore}</span>
                        <span className="swap-review-arrow">→</span>
                        <span className="swap-review-state after">{pair.requesterAfter}</span>
                      </div>
                    </div>
                    <div className="swap-review-person-card">
                      <span className="swap-review-person-name">{data.targetUserDisplayName}</span>
                      <div className="swap-review-flow">
                        <span className="swap-review-state before">{pair.targetBefore}</span>
                        <span className="swap-review-arrow">→</span>
                        <span className="swap-review-state after">{pair.targetAfter}</span>
                      </div>
                    </div>
                  </div>
                </section>
              ))}
            </div>
            {message && <div className="alert success">{message}</div>}
            {canReview && (
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
