import { createPortal } from 'react-dom';
import {
  Children,
  isValidElement,
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type ReactNode,
} from 'react';
import gsap from 'gsap';
import { cn } from '../../lib/cn';

export type SelectOption = {
  value: string;
  label: string;
  disabled?: boolean;
};

type SelectProps = {
  value: string;
  onChange: (nextValue: string) => void;
  options?: SelectOption[];
  children?: ReactNode;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
  panelClassName?: string;
  ariaLabel?: string;
  searchable?: boolean;
  searchPlaceholder?: string;
};

export function Select({
  value,
  onChange,
  options,
  children,
  placeholder = 'Select',
  disabled = false,
  className,
  panelClassName,
  ariaLabel,
  searchable = false,
  searchPlaceholder = 'Search',
}: SelectProps) {
  const buttonRef = useRef<HTMLButtonElement | null>(null);
  const panelRef = useRef<HTMLDivElement | null>(null);
  const searchInputRef = useRef<HTMLInputElement | null>(null);
  const listboxId = useId();
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const [position, setPosition] = useState({ top: 0, left: 0, width: 0 });
  const [query, setQuery] = useState('');

  const resolvedOptions = useMemo<SelectOption[]>(() => {
    if (options?.length) return options;

    const derived: SelectOption[] = [];
    Children.forEach(children, (child) => {
      if (!isValidElement(child) || child.type !== 'option') return;
      const optionProps = child.props as {
        value?: string;
        children?: ReactNode;
        disabled?: boolean;
      };
      derived.push({
        value: String(optionProps.value ?? ''),
        label: String(optionProps.children ?? ''),
        disabled: Boolean(optionProps.disabled),
      });
    });
    return derived;
  }, [children, options]);

  const normalizedQuery = query.trim().toLowerCase();
  const visibleOptions = useMemo(
    () => searchable && normalizedQuery
      ? resolvedOptions.filter((option) => option.label.toLowerCase().includes(normalizedQuery))
      : resolvedOptions,
    [normalizedQuery, resolvedOptions, searchable],
  );
  const enabledOptions = useMemo(
    () => visibleOptions.filter((option) => !option.disabled),
    [visibleOptions],
  );
  const selectedOption = resolvedOptions.find((option) => option.value === value) ?? null;

  const updatePosition = () => {
    const rect = buttonRef.current?.getBoundingClientRect();
    if (!rect) return;
    setPosition({
      top: rect.bottom + 8,
      left: rect.left,
      width: rect.width,
    });
  };

  useEffect(() => {
    if (!open) return;

    updatePosition();
    setQuery('');
    if (searchable) {
      window.setTimeout(() => searchInputRef.current?.focus(), 0);
    }

    const handlePointerDown = (event: MouseEvent) => {
      const target = event.target as Node;
      if (buttonRef.current?.contains(target) || panelRef.current?.contains(target)) return;
      setOpen(false);
    };

    const handleResize = () => updatePosition();
    const handleScroll = () => updatePosition();

    document.addEventListener('mousedown', handlePointerDown);
    window.addEventListener('resize', handleResize);
    window.addEventListener('scroll', handleScroll, true);

    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      window.removeEventListener('resize', handleResize);
      window.removeEventListener('scroll', handleScroll, true);
    };
  }, [open]);

  useEffect(() => {
    if (!open) return;

    const selectedIndex = visibleOptions.findIndex(
      (option) => option.value === value && !option.disabled,
    );
    setActiveIndex(
      selectedIndex >= 0
        ? selectedIndex
        : visibleOptions.findIndex((option) => !option.disabled),
    );
  }, [open, value, visibleOptions]);

  useLayoutEffect(() => {
    if (!open || !panelRef.current) return;

    const ctx = gsap.context(() => {
      gsap.fromTo(
        panelRef.current,
        { autoAlpha: 0, y: -6, scale: 0.98 },
        { autoAlpha: 1, y: 0, scale: 1, duration: 0.18, ease: 'power2.out' },
      );
    }, panelRef);

    return () => ctx.revert();
  }, [open]);

  const selectValue = (nextValue: string) => {
    onChange(nextValue);
    setOpen(false);
    buttonRef.current?.focus();
  };

  const moveActive = (direction: 1 | -1) => {
    if (!enabledOptions.length) return;

    const current =
      activeIndex >= 0 ? activeIndex : visibleOptions.findIndex((option) => !option.disabled);
    let next = current;
    do {
      next += direction;
      if (next < 0) next = visibleOptions.length - 1;
      if (next >= visibleOptions.length) next = 0;
    } while (visibleOptions[next]?.disabled && next !== current);

    setActiveIndex(next);
  };

  const handleKeyDown = (event: ReactKeyboardEvent<HTMLButtonElement>) => {
    if (disabled) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      if (!open) {
        setOpen(true);
        return;
      }
      moveActive(1);
      return;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      if (!open) {
        setOpen(true);
        return;
      }
      moveActive(-1);
      return;
    }

    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      if (!open) {
        setOpen(true);
        return;
      }

      const candidate = visibleOptions[activeIndex];
      if (candidate && !candidate.disabled) {
        selectValue(candidate.value);
      }
      return;
    }

    if (event.key === 'Escape') {
      setOpen(false);
    }
  };

  return (
    <>
      <button
        ref={buttonRef}
        type="button"
        className={cn('st-select-trigger', open && 'open', disabled && 'disabled', className)}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listboxId}
        aria-label={ariaLabel}
        data-ui-cursor="hand"
        disabled={disabled}
        onClick={() => {
          if (disabled) return;
          updatePosition();
          setOpen((current) => !current);
        }}
        onKeyDown={handleKeyDown}
      >
        <span className={cn('st-select-value', !selectedOption && 'placeholder')}>
          {selectedOption?.label ?? placeholder}
        </span>
        <span className={cn('st-select-caret', open && 'open')} aria-hidden="true">
          ▾
        </span>
      </button>

      {open && typeof document !== 'undefined'
        ? createPortal(
            <div
              ref={panelRef}
              id={listboxId}
              role="listbox"
              className={cn('st-select-popover', panelClassName)}
              style={{ top: position.top, left: position.left, width: position.width }}
            >
              {searchable && (
                <div className="st-select-search-wrap">
                  <input
                    ref={searchInputRef}
                    className="st-select-search"
                    type="search"
                    value={query}
                    placeholder={searchPlaceholder}
                    aria-label={searchPlaceholder}
                    onChange={(event) => setQuery(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === 'Escape') {
                        event.preventDefault();
                        setOpen(false);
                        buttonRef.current?.focus();
                      }
                    }}
                  />
                </div>
              )}
              {visibleOptions.length ? visibleOptions.map((option, index) => {
                const selected = option.value === value;
                const active = index === activeIndex;
                return (
                  <button
                    key={option.value || `option-${index}`}
                    type="button"
                    role="option"
                    aria-selected={selected}
                    className={cn(
                      'st-select-option',
                      selected && 'selected',
                      active && 'active',
                      option.disabled && 'disabled',
                    )}
                    disabled={option.disabled}
                    data-ui-cursor="hand"
                    onMouseEnter={() => setActiveIndex(index)}
                    onClick={() => selectValue(option.value)}
                  >
                    <span className="st-select-option-label">{option.label}</span>
                    {selected && <span className="st-select-option-check">✓</span>}
                  </button>
                );
              }) : (
                <div className="st-select-empty">No results</div>
              )}
            </div>,
            document.body,
          )
        : null}
    </>
  );
}
