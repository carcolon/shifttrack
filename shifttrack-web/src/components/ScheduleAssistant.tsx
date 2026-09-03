import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { apiFetch } from '../lib/api';
import { Button } from './ui/Button';
import trackyImage from '../assets/tracky.png';

type AssistantCalendarFact = {
  type: string;
  date: string;
  label: string;
};

type AssistantEmployeeResult = {
  employeeId: string;
  displayName: string;
  email: string;
  facts: AssistantCalendarFact[];
};

type AssistantQueryResponse = {
  intent: string;
  status: string;
  message: string;
  weekStart: string;
  matches: AssistantEmployeeResult[];
};

type ChatMessage =
  | { id: string; role: 'user'; text: string }
  | { id: string; role: 'assistant'; text: string; payload?: AssistantQueryResponse };

type HelpSection = {
  title: string;
  note?: string;
  prompts: string[];
};

const helpSections: HelpSection[] = [
  {
    title: 'Days Off / Dias Libres',
    prompts: [
      'Days off of Jhon',
      'Is Jhon Smith off on Friday?',
      'Who has free days next week?',
      'Quien tiene dias libres la siguiente semana?',
      'Quien no trabaja el viernes?',
      'Esta Jhon Doe libre manana?',
      'Who has days off for managers in col?',
      'Who is off on March 17 2026?',
      'Quien tiene dias libres el viernes?',
    ],
  },
  {
    title: 'Schedule / Horario',
    prompts: [
      'Schedule of Jhon Doe next week',
      'Does Jhon Doe work next monday?',
      'Who works on March 17 2026?',
      'Quien trabaja el 17 de marzo de 2026?',
      'Who works tomorrow?',
      'Who are in the morning shift?',
      'Who works in the late shift on March 17 2026?',
      'Quien trabaja en el turno de la manana?',
      'Quien trabaja en el turno de la tarde el 17 de marzo de 2026?',
      'Who works in leaders next monday?',
      'Quien trabaja en outbound el lunes?',
      'What is Jhon Doe schedule on Tuesday?',
      'Que horario tiene Jhon Doe el martes?',
    ],
  },
  {
    title: 'PTO',
    prompts: [
      'Who was on PTO last week?',
      'Who has PTO week of 2026-03-16?',
      'Who has PTO on March 11 2026?',
      'Quien tiene PTO semana del 16 de marzo de 2026?',
      'Is Jhon Doe on PTO on March 11 2026?',
      'Who has PTO in leaders this week?',
      'Vacaciones de Jhon Doe',
      'When is Jhon Doe on PTO?',
    ],
  },
  {
    title: 'Status / Estado',
    prompts: [
      'Is Jhon Doe active?',
      'Who is inactive?',
      'Esta Jhon Doe inactivo?',
      'Who are active?',
      'Quienes estan activos?',
    ],
  },
  {
    title: 'Weeks / Semanas',
    note: 'Tracky uses the selected calendar week by default, but you can override it inside a full question.',
    prompts: [
      'Who has PTO this week?',
      'Who has days off next week?',
      'Who was on PTO last week?',
      'Quien tiene PTO esta semana?',
      'Quien tiene dias libres la siguiente semana?',
      'Quien estuvo en PTO la semana pasada?',
      'Who has PTO week of 2026-03-16?',
      'Who works week of March 16 2026?',
      'Quien tiene PTO semana del 16 de marzo de 2026?',
    ],
  },
  {
    title: 'Dates / Fechas',
    note: 'Use dates and relative dates as part of a full question.',
    prompts: [
      'Who works on 2026-03-17?',
      'Who works on 17/03/2026?',
      'Who works on March 17 2026?',
      'Quien trabaja el 17 de marzo de 2026?',
      'Who works today?',
      'Who works tomorrow?',
      'Quien esta libre manana?',
      'Quien tuvo PTO ayer?',
    ],
  },
  {
    title: 'Filters / Filtros',
    note: 'Natural filters work in English and Spanish, without sensitivity to case or accents.',
    prompts: [
      'who are leaders?',
      'quien pertenece a leaders?',
      'quien pertenece a esquire law?',
      'Who belongs to outbound?',
      'Quien pertenece a COL?',
      'Who are managers in Esquire Law?',
      'Quienes son administradores en leaders?',
      'Who are in the morning shift this week?',
      'Quien trabaja en el turno de la manana esta semana?',
      'Quien trabaja en el turno de la tarde el viernes?',
      'Who works in leaders next monday?',
      'Quien trabaja en col rol manager?',
      'Who has PTO company Esquire Law this week?',
    ],
  },
];

const factClassFor = (type: string) => {
  if (type === 'day_off') return 'assistant-fact dayoff';
  if (type === 'pto') return 'assistant-fact pto';
  if (type === 'active' || type === 'inactive') return 'assistant-fact status';
  return 'assistant-fact working';
};

export function ScheduleAssistant({
  weekStart,
  onPromptReady,
  docked = false,
}: {
  weekStart: string;
  onPromptReady?: (submit: (prompt: string) => Promise<void>) => void;
  docked?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [helpOpen, setHelpOpen] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: 'welcome',
      role: 'assistant',
      text: 'Ask in English or Spanish about days off, schedules, PTO, who works on a weekday or exact date, employee status, or a specific week.',
    },
  ]);

  const submitPrompt = useCallback(async (prompt: string) => {
    const normalized = prompt.trim();
    if (!normalized) return;

    setOpen(true);
    setMessages((current) => [...current, { id: `u-${Date.now()}`, role: 'user', text: normalized }]);
    setInput('');
    setLoading(true);

    try {
      const res = await apiFetch('/assistant/query', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: normalized, weekStart }),
      });
      if (!res.ok) {
        const payload = (await res.json().catch(() => null)) as { message?: string } | null;
        throw new Error(payload?.message ?? 'Unable to query the assistant.');
      }

      const payload = (await res.json()) as AssistantQueryResponse;
      setMessages((current) => [
        ...current,
        {
          id: `a-${Date.now()}`,
          role: 'assistant',
          text: payload.message,
          payload,
        },
      ]);
    } catch (error: any) {
      setMessages((current) => [
        ...current,
        {
          id: `a-${Date.now()}`,
          role: 'assistant',
          text: error?.message ?? 'Unable to query the assistant.',
        },
      ]);
    } finally {
      setLoading(false);
    }
  }, [weekStart]);

  useEffect(() => {
    if (!onPromptReady) return;
    onPromptReady(submitPrompt);
  }, [onPromptReady, submitPrompt]);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    await submitPrompt(input);
  };

  return (
    <div className={`assistant-widget ${open ? 'open' : ''} ${docked ? 'docked' : ''}`} aria-label="Schedule assistant">
      <Button type="button" className="assistant-launcher" variant="ghost" onClick={() => setOpen((value) => !value)}>
        <span className="assistant-launcher-avatar">
          <img src={trackyImage} alt="Tracky" />
        </span>
        <span className="assistant-launcher-copy">
          <span className="assistant-launcher-title">Tracky</span>
          <span className="assistant-launcher-subtitle">your schedule assistant</span>
        </span>
      </Button>

      {open && (
        <section className="assistant-panel">
          <div className="assistant-panel-head">
            <div className="assistant-panel-brand">
              <span className="assistant-panel-brand-mark">
                <img src={trackyImage} alt="Tracky" />
              </span>
              <div>
                <h3>Tracky</h3>
                <strong>your schedule assistant</strong>
              </div>
            </div>
            <div className="assistant-panel-copy">
              <p>Week context: {weekStart}. Use this week, next week, last week, week of 2026-03-16, week of March 16 2026, or semana del 16 de marzo de 2026.</p>
            </div>
            <Button type="button" className="assistant-close" variant="ghost" size="sm" onClick={() => setOpen(false)} aria-label="Close assistant">
              x
            </Button>
          </div>

          <div className="assistant-feed">
            {messages.map((message) => (
              <div key={message.id} className={`assistant-bubble ${message.role}`}>
                <div className="assistant-text">{message.text}</div>
                {message.role === 'assistant' && message.payload?.matches?.length ? (
                  <div className="assistant-results">
                    {message.payload.matches.map((match) => (
                      <div key={match.employeeId} className="assistant-result-card">
                        <div className="assistant-result-head">
                          <strong>{match.displayName}</strong>
                          <span>{match.email}</span>
                        </div>
                        <div className="assistant-result-body">
                          {match.facts.length > 0 ? (
                            match.facts.map((fact) => (
                              <span key={`${match.employeeId}-${fact.date}-${fact.label}`} className={factClassFor(fact.type)}>
                                {fact.label}
                              </span>
                            ))
                          ) : (
                            <span className="assistant-empty">No matching items in this week.</span>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : null}
              </div>
            ))}
            {loading && (
              <div className="assistant-bubble assistant">
                <div className="assistant-text">Thinking...</div>
              </div>
            )}
          </div>

          <section className="assistant-examples-panel">
            <Button
              type="button"
              className="assistant-examples-toggle"
              variant="ghost"
              onClick={() => setHelpOpen(true)}
              aria-haspopup="dialog"
              aria-expanded={helpOpen}
            >
              <span>What You Can Ask</span>
              <span className="assistant-examples-indicator">?</span>
            </Button>
          </section>

          <form className="assistant-form" onSubmit={onSubmit}>
            <input
              type="text"
              className="assistant-input"
              value={input}
              onChange={(event) => setInput(event.target.value)}
              placeholder="Ask about dates, weeks, schedule, PTO, days off, or employee status..."
              disabled={loading}
            />
            <Button type="submit" disabled={loading || !input.trim()}>
              Send
            </Button>
          </form>

          {helpOpen && (
            <div className="assistant-help-overlay" role="dialog" aria-modal="true" aria-label="Tracky help">
              <div className="assistant-help-modal">
                <div className="assistant-help-head">
                  <div>
                    <strong>Everything Tracky Can Do</strong>
                    <p>Complete rule inventory for schedules, days off, PTO, status, dates, weeks, shifts, and natural filters.</p>
                  </div>
                  <Button type="button" className="assistant-help-close" variant="ghost" size="sm" onClick={() => setHelpOpen(false)} aria-label="Close help">
                    x
                  </Button>
                </div>

                <div className="assistant-help-body">
                  {helpSections.map((section) => (
                    <section key={section.title} className="assistant-help-section">
                      <div className="assistant-help-section-head">
                        <h4>{section.title}</h4>
                        {section.note ? <p>{section.note}</p> : null}
                      </div>
                      <div className="assistant-example-group-chips">
                        {section.prompts.map((prompt) => (
                          <Button
                            key={`${section.title}-${prompt}`}
                            type="button"
                            className="assistant-example-chip"
                            variant="ghost"
                            size="sm"
                            onClick={() => submitPrompt(prompt)}
                            disabled={loading}
                          >
                            {prompt}
                          </Button>
                        ))}
                      </div>
                    </section>
                  ))}
                </div>
              </div>
            </div>
          )}
        </section>
      )}
    </div>
  );
}

export default ScheduleAssistant;
