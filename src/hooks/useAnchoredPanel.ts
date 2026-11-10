'use client'

import { useCallback, useEffect, useRef, useState } from 'react'

export interface AnchoredPanelPosition {
  x: number
  y: number
  width?: number
}

interface UseAnchoredPanelOptions {
  /** Gap between the trigger and the panel, in px. */
  offset?: number
  /** Mirror the trigger width onto the panel (used by search boxes). */
  matchWidth?: boolean
}

/**
 * Keeps a `position: fixed` panel glued to its trigger.
 *
 * The position is measured in the same event that opens the panel, so the
 * panel's very first paint is already at the trigger instead of flashing at
 * the top-left corner of the viewport (0, 0) for a frame. While open it is
 * re-measured on scroll/resize so it does not drift away from the trigger.
 *
 * Render the panel only when `open && position` is set.
 */
export function useAnchoredPanel<T extends HTMLElement = HTMLButtonElement>({
  offset = 4,
  matchWidth = false,
}: UseAnchoredPanelOptions = {}) {
  const [open, setOpen] = useState(false)
  const [position, setPosition] = useState<AnchoredPanelPosition | null>(null)
  const triggerRef = useRef<T | null>(null)
  const panelRef = useRef<HTMLDivElement | null>(null)

  const measure = useCallback((): AnchoredPanelPosition | null => {
    const trigger = triggerRef.current
    if (!trigger) return null
    const rect = trigger.getBoundingClientRect()
    const next: AnchoredPanelPosition = { x: rect.left, y: rect.bottom + offset }
    if (matchWidth) next.width = rect.width
    return next
  }, [offset, matchWidth])

  const openPanel = useCallback(() => {
    const next = measure()
    if (!next) return
    setPosition(next)
    setOpen(true)
  }, [measure])

  const closePanel = useCallback(() => setOpen(false), [])

  const togglePanel = useCallback(() => {
    if (open) closePanel()
    else openPanel()
  }, [open, closePanel, openPanel])

  useEffect(() => {
    if (!open) return

    const handlePointerDown = (event: MouseEvent) => {
      const target = event.target as Node
      if (triggerRef.current?.contains(target)) return
      if (panelRef.current?.contains(target)) return
      // Menus rendered into #overlay-root manage their own dismissal.
      if (document.getElementById('overlay-root')?.contains(target)) return
      closePanel()
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') closePanel()
    }

    document.addEventListener('mousedown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('mousedown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [open, closePanel])

  useEffect(() => {
    if (!open) return

    const reposition = () => {
      const next = measure()
      if (next) setPosition(next)
    }

    window.addEventListener('scroll', reposition, true)
    window.addEventListener('resize', reposition)
    return () => {
      window.removeEventListener('scroll', reposition, true)
      window.removeEventListener('resize', reposition)
    }
  }, [open, measure])

  return { open, position, triggerRef, panelRef, openPanel, closePanel, togglePanel }
}
