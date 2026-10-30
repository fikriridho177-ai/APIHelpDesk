import { useEffect, useMemo, useRef, useState } from 'react'
import type { ScopeId, ScopeOption } from '../types/group'

type Props = {
  all: boolean
  allLabel: string
  options: ScopeOption[]
  selectedIds: ScopeId[]
  onAllChange: (value: boolean) => void
  onSelectedChange: (ids: ScopeId[]) => void
  disabled?: boolean
}

export default function ScopeMultiSelect({
  all,
  allLabel,
  options,
  selectedIds,
  onAllChange,
  onSelectedChange,
  disabled = false,
}: Props) {
  const [search, setSearch] = useState('')
  const [open, setOpen] = useState(false)
  const wrapperRef = useRef<HTMLDivElement | null>(null)

  const selected = useMemo(
    () => new Set(selectedIds),
    [selectedIds],
  )

  const selectedOptions = useMemo(
    () => options.filter(option => selected.has(option.id)),
    [options, selected],
  )

  const filtered = useMemo(() => {
    const keyword = search.trim().toLowerCase()

    if (!keyword) return options

    return options.filter(option =>
      `${option.code} ${option.name} ${option.extra ?? ''}`
        .toLowerCase()
        .includes(keyword),
    )
  }, [options, search])

  useEffect(() => {
    function handleOutsideClick(event: MouseEvent) {
      if (
        wrapperRef.current &&
        !wrapperRef.current.contains(event.target as Node)
      ) {
        setOpen(false)
        setSearch('')
      }
    }

    function handleEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setOpen(false)
        setSearch('')
      }
    }

    document.addEventListener('mousedown', handleOutsideClick)
    document.addEventListener('keydown', handleEscape)

    return () => {
      document.removeEventListener('mousedown', handleOutsideClick)
      document.removeEventListener('keydown', handleEscape)
    }
  }, [])

  useEffect(() => {
    if (all || disabled) {
      setOpen(false)
      setSearch('')
    }
  }, [all, disabled])

  function toggle(id: ScopeId) {
    if (disabled || all) return

    if (selected.has(id)) {
      onSelectedChange(
        selectedIds.filter(selectedId => selectedId !== id),
      )
      return
    }

    onSelectedChange([...selectedIds, id])
  }

  function removeSelected(id: ScopeId) {
    if (disabled || all) return

    onSelectedChange(
      selectedIds.filter(selectedId => selectedId !== id),
    )
  }

  function handleAllChange(value: boolean) {
    onAllChange(value)
    setOpen(false)
    setSearch('')
  }

  const emptyMessage =
    options.length === 0
      ? 'Data master belum tersedia.'
      : 'Data tidak ditemukan.'

  return (
    <div className="scope-selector">
      <label className="scope-check-all">
        <input
          type="checkbox"
          checked={all}
          disabled={disabled}
          onChange={event => handleAllChange(event.target.checked)}
        />
        <span>{allLabel}</span>
      </label>

      <div
        ref={wrapperRef}
        className={
          `scope-multiselect${open ? ' open' : ''}${all || disabled ? ' disabled' : ''}`
        }
      >
        <button
          type="button"
          className="scope-multiselect-control"
          disabled={all || disabled}
          onClick={() => setOpen(current => !current)}
          aria-expanded={open}
        >
          <div className="scope-multiselect-value">
            {all ? (
              <span className="scope-multiselect-placeholder">
                Semua data aktif dapat diakses
              </span>
            ) : selectedOptions.length > 0 ? (
              <div className="scope-chips-inline">
                {selectedOptions.map(option => (
                  <span key={option.id} className="scope-chip-inline">
                    <span>{option.code} - {option.name}{option.extra ? ` - ${option.extra}` : ''}</span>
                    {!disabled && (
                      <span
                        className="scope-chip-remove"
                        role="button"
                        tabIndex={0}
                        aria-label={`Hapus ${option.code} - ${option.name}${option.extra ? ` - ${option.extra}` : ''}`}
                        onClick={event => {
                          event.stopPropagation()
                          removeSelected(option.id)
                        }}
                        onKeyDown={event => {
                          if (event.key === 'Enter' || event.key === ' ') {
                            event.preventDefault()
                            event.stopPropagation()
                            removeSelected(option.id)
                          }
                        }}
                      >
                        ×
                      </span>
                    )}
                  </span>
                ))}
              </div>
            ) : (
              <span className="scope-multiselect-placeholder">
                Klik untuk mencari dan memilih data...
              </span>
            )}
          </div>

          <span className="scope-multiselect-arrow">
            {open ? '▲' : '▼'}
          </span>
        </button>

        {open && !all && !disabled && (
          <div className="scope-multiselect-dropdown">
            <div className="scope-multiselect-search-wrap">
              <input
                autoFocus
                className="scope-multiselect-search"
                value={search}
                onChange={event => setSearch(event.target.value)}
                placeholder="Cari kode atau nama..."
                onClick={event => event.stopPropagation()}
              />
            </div>

            <div className="scope-multiselect-options">
              {filtered.length === 0 ? (
                <div className="scope-empty">{emptyMessage}</div>
              ) : (
                filtered.map(option => {
                  const isSelected = selected.has(option.id)

                  return (
                    <button
                      key={option.id}
                      type="button"
                      className={
                        `scope-multiselect-option${isSelected ? ' selected' : ''}`
                      }
                      onClick={() => toggle(option.id)}
                    >
                      <span className="scope-option-columns">
                        <strong className="scope-option-code">{option.code}</strong>
                        <span className="scope-option-name">{option.name}</span>
                        <span className="scope-option-extra">{option.extra ?? '-'}</span>
                      </span>
                    </button>
                  )
                })
              )}
            </div>
          </div>
        )}
      </div>

      {!all && selectedOptions.length > 0 && (
        <div className="scope-selected-summary">
          {selectedOptions.length} data dipilih
        </div>
      )}
    </div>
  )
}
