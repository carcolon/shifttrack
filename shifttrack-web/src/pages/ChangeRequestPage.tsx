import { useEffect, useMemo, useState } from 'react';
import { Button } from '../components/ui/Button';
import { ErrorPopup } from '../components/ui/ErrorPopup';
import { Select } from '../components/ui/Select';
import { apiFetch } from '../lib/api';
import { roleLabelForValue } from '../lib/roles';
import type { ApiError, SwapCandidate, UserInfo } from '../types';

const requestTypeOptions = [{ value: 'swap_shift', label: 'Swap Shift' }];

const sortDates = (values: string[]) => Array.from(new Set(values.filter(Boolean))).sort((a, b) => a.localeCompare(b));

export function ChangeRequestPage({ user }: { user: UserInfo }) {
  const [requesterDateInput, setRequesterDateInput] = useState('');
  const [targetDateInput, setTargetDateInput] = useState('');
  const [requesterDates, setRequesterDates] = useState<string[]>([]);
  const [targetDates, setTargetDates] = useState<string[]>([]);
  const [requestType, setRequestType] = useState('swap_shift');
  const [targetUserId, setTargetUserId] = useState('');
  const [candidateQuery, setCandidateQuery] = useState('');
  const [comments, setComments] = useState('');
  const [candidates, setCandidates] = useState<SwapCandidate[]>([]);
  const [loadingCandidates, setLoadingCandidates] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const roleLabel = useMemo(() => roleLabelForValue(user.role), [user.role]);

  useEffect(() => {
    if (targetDates.length === 0) {
      setCandidates([]);
      setTargetUserId('');
      setCandidateQuery('');
      return;
    }

    let cancelled = false;
    const loadCandidates = async () => {
      setLoadingCandidates(true);
      setError(null);
      try {
        const params = new URLSearchParams();
        targetDates.forEach((date) => params.append('targetDates', date));
        const res = await apiFetch(`/swap/candidates?${params.toString()}`);
        const json = (await res.json().catch(() => null)) as ApiError | SwapCandidate[] | null;
        if (!res.ok) throw new Error((json as ApiError | null)?.message ?? 'Unable to load swap candidates.');
        if (cancelled) return;

        const items = (json as SwapCandidate[]) ?? [];
        setCandidates(items);
        setTargetUserId((current) => {
          const match = items.find((item) => item.id === current);
          if (match) {
            setCandidateQuery(`${match.displayName} - ${match.email}`);
            return current;
          }
          setCandidateQuery('');
          return '';
        });
      } catch (e: any) {
        if (!cancelled) {
          setCandidates([]);
          setTargetUserId('');
          setCandidateQuery('');
          setError(e.message ?? 'Unable to load swap candidates.');
        }
      } finally {
        if (!cancelled) {
          setLoadingCandidates(false);
        }
      }
    };

    void loadCandidates();
    return () => {
      cancelled = true;
    };
  }, [targetDates]);

  const filteredCandidates = useMemo(() => {
    const term = candidateQuery.trim().toLowerCase();
    if (!term) return candidates;
    return candidates.filter((candidate) =>
      [`${candidate.displayName} - ${candidate.email}`, candidate.displayName, candidate.email, candidate.shiftTime, candidate.shiftLabel]
        .filter(Boolean)
        .some((value) => value.toLowerCase().includes(term))
    );
  }, [candidateQuery, candidates]);

  const addDate = (side: 'requester' | 'target') => {
    const value = side === 'requester' ? requesterDateInput : targetDateInput;
    if (!value) return;

    if (side === 'requester') {
      setRequesterDates((current) => sortDates([...current, value]));
      setRequesterDateInput('');
    } else {
      setTargetDates((current) => sortDates([...current, value]));
      setTargetDateInput('');
      setTargetUserId('');
      setCandidateQuery('');
    }
  };

  const removeDate = (side: 'requester' | 'target', value: string) => {
    if (side === 'requester') {
      setRequesterDates((current) => current.filter((item) => item !== value));
    } else {
      setTargetDates((current) => current.filter((item) => item !== value));
      setTargetUserId('');
      setCandidateQuery('');
    }
  };

  const selectCandidate = (value: string) => {
    setCandidateQuery(value);
    const exact = candidates.find((candidate) => `${candidate.displayName} - ${candidate.email}` === value);
    setTargetUserId(exact?.id ?? '');
  };

  const submit = async () => {
    setError(null);
    setSuccess(null);

    if (requesterDates.length === 0 || targetDates.length === 0) {
      setError('Please add at least one date on both sides of the swap.');
      return;
    }

    if (requesterDates.length !== targetDates.length) {
      setError('Your dates and the selected employee dates must have the same number of days.');
      return;
    }

    if (!targetUserId) {
      setError('Please choose the person to swap with.');
      return;
    }

    if (!comments.trim()) {
      setError('Please add observations for the swap request.');
      return;
    }

    setSaving(true);
    try {
      const res = await apiFetch('/swap/requests', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          targetUserId,
          requestedDates: requesterDates,
          targetDates,
          requestType,
          comments: comments.trim() || null,
        }),
      });

      const json = (await res.json().catch(() => null)) as ApiError | null;
      if (!res.ok) throw new Error(json?.message ?? 'Unable to create swap request.');

      setSuccess('Swap request submitted. The selected employee will receive an email to review the full schedule change.');
      setRequesterDates([]);
      setTargetDates([]);
      setTargetUserId('');
      setCandidateQuery('');
      setComments('');
    } catch (e: any) {
      setError(e.message ?? 'Unable to create swap request.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="card">
      <div className="card-header">
        <div className="member-scope-wrap">
          <h2>Change Request</h2>
        </div>
        <Button variant="primary" onClick={submit} disabled={saving}>
          {saving ? 'Saving...' : 'Save'}
        </Button>
      </div>

      <p className="helper">Create a swap request with another active {roleLabel.toLowerCase()} using your date list against theirs.</p>
      <ErrorPopup message={error} onClose={() => setError(null)} title="Change request error" />
      {success && <div className="success-banner">{success}</div>}

      <div className="change-request-layout">
        <section className="change-request-section">
          <h3>Schedule Changes Request</h3>
          <div className="change-request-grid">
            <label className="field">
              <span>Your date(s)*</span>
              <div className="change-request-date-row">
                <input type="date" value={requesterDateInput} onChange={(e) => setRequesterDateInput(e.target.value)} />
                <Button type="button" variant="ghost" onClick={() => addDate('requester')}>
                  Add
                </Button>
              </div>
              <div className="change-request-chip-list">
                {requesterDates.map((date) => (
                  <button key={date} type="button" className="change-request-chip" onClick={() => removeDate('requester', date)}>
                    {date} ×
                  </button>
                ))}
              </div>
            </label>

            <label className="field">
              <span>Request Type*</span>
              <Select value={requestType} onChange={setRequestType} ariaLabel="Request Type">
                {requestTypeOptions.map((item) => (
                  <option key={item.value} value={item.value}>
                    {item.label}
                  </option>
                ))}
              </Select>
            </label>
          </div>
        </section>

        <section className="change-request-section">
          <h3>Swap Information</h3>
          <div className="change-request-grid">
            <label className="field">
              <span>Selected employee date(s)*</span>
              <div className="change-request-date-row">
                <input type="date" value={targetDateInput} onChange={(e) => setTargetDateInput(e.target.value)} />
                <Button type="button" variant="ghost" onClick={() => addDate('target')}>
                  Add
                </Button>
              </div>
              <div className="change-request-chip-list">
                {targetDates.map((date) => (
                  <button key={date} type="button" className="change-request-chip" onClick={() => removeDate('target', date)}>
                    {date} ×
                  </button>
                ))}
              </div>
            </label>

            <label className="field">
              <span>Choose person to swap with*</span>
              <input
                list="swap-candidate-options"
                type="text"
                value={candidateQuery}
                onChange={(e) => selectCandidate(e.target.value)}
                placeholder="Type a name or email"
                disabled={targetDates.length === 0 || loadingCandidates}
              />
              <datalist id="swap-candidate-options">
                {filteredCandidates.map((candidate) => (
                  <option key={candidate.id} value={`${candidate.displayName} - ${candidate.email}`}>
                    {candidate.shiftLabel}
                  </option>
                ))}
              </datalist>
            </label>

            <label className="field">
              <span>Observations*</span>
              <input
                type="text"
                value={comments}
                onChange={(e) => setComments(e.target.value)}
                placeholder="Add context for the swap"
              />
            </label>
          </div>
          {!loadingCandidates && targetDates.length > 0 && candidates.length === 0 && (
            <p className="helper">No eligible same-role employees with working shifts were found for the selected employee date list.</p>
          )}
          {!loadingCandidates && targetDates.length > 0 && candidateQuery.trim() && !targetUserId && candidates.length > 0 && filteredCandidates.length === 0 && (
            <p className="helper">No same-role employee matches that search. Try another name or email.</p>
          )}
          {requesterDates.length > 0 && targetDates.length > 0 && requesterDates.length !== targetDates.length && (
            <p className="helper">Both sides of the swap must contain the same number of dates.</p>
          )}
        </section>
      </div>
    </div>
  );
}

export default ChangeRequestPage;
