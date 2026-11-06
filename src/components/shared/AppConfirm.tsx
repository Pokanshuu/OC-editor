'use client'

import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'

interface AppConfirmProps {
  open: boolean
  title: string
  description?: string
  confirmLabel?: string
  cancelLabel?: string
  danger?: boolean
  busy?: boolean
  onConfirm: () => void
  onCancel: () => void
}

export function AppConfirm({
  open,
  title,
  description,
  confirmLabel = '确认',
  cancelLabel = '取消',
  danger = false,
  busy = false,
  onConfirm,
  onCancel,
}: AppConfirmProps) {
  const [mounted, setMounted] = useState(false)

  useEffect(() => {
    setMounted(true)
    if (!open) return
    const onKey = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') onCancel()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [open, onCancel])

  if (!open || !mounted) return null
  // 必须挂到 #overlay-root 里：它自带 z-index:999999 的层叠上下文，
  // 挂到 body 上的确认框（即使 z-10000）会被里面的全屏遮罩盖住，按钮点不到。
  const root = document.getElementById('overlay-root') ?? document.body

  return createPortal(
    <div
      className="pointer-events-auto fixed inset-0 z-[1000000] flex items-center justify-center bg-black/25 p-4"
      onClick={(event) => {
        if (event.target === event.currentTarget) onCancel()
      }}
    >
      <div
        className="w-[360px] rounded-xl border border-line bg-paper p-4 shadow-xl"
        onPointerDown={(event) => event.stopPropagation()}
        onClick={(event) => event.stopPropagation()}
      >
        <h3 className="font-serif text-base text-ink">{title}</h3>
        {description ? (
          <p className="mt-1 text-[11px] leading-5 text-ink-muted">{description}</p>
        ) : null}
        <div className="mt-4 flex justify-end gap-2">
          <button
            onClick={onCancel}
            disabled={busy}
            className="h-8 rounded-lg border border-line px-3 text-xs text-ink-muted hover:border-line-hover hover:text-ink disabled:opacity-40"
          >
            {cancelLabel}
          </button>
          <button
            onClick={onConfirm}
            disabled={busy}
            className={`h-8 rounded-lg px-3 text-xs text-paper hover:opacity-90 disabled:opacity-50 ${
              danger ? 'bg-error' : 'bg-ink'
            }`}
          >
            {busy ? '处理中…' : confirmLabel}
          </button>
        </div>
      </div>
    </div>,
    root,
  )
}
