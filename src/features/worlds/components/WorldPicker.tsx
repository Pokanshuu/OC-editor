'use client'

import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { createPortal } from 'react-dom'
import { BookOpen, Check, Globe2, Pencil, Plus, Trash2, X } from 'lucide-react'
import { AppConfirm } from '@/components/shared/AppConfirm'
import type { World } from '@/types'
import { useWorlds } from '../hooks/useWorlds'
import { countWorldContents, type WorldContentCounts } from '../services'

interface WorldPickerProps {
  /** gate：首次进入时整页选择；manage：浮层管理（重命名/删除）。 */
  mode?: 'gate' | 'manage'
  onClose?: () => void
}

function formatDate(timestamp: number): string {
  const date = new Date(timestamp)
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function CountLine({ counts }: { counts?: WorldContentCounts }) {
  if (!counts) return <span className="text-[11px] text-ink-faint">统计中…</span>
  return (
    <span className="flex flex-wrap items-center gap-x-2 gap-y-0.5 text-[11px] text-ink-faint">
      <span>角色 {counts.characters}</span>
      <span>事件 {counts.events}</span>
      <span>国家 {counts.countries}</span>
      <span>词条 {counts.entries}</span>
      <span>关系 {counts.relations}</span>
    </span>
  )
}

function WorldCard({
  world,
  counts,
  active,
  manage,
  canDelete,
  onEnter,
  onRename,
  onDelete,
}: {
  world: World
  counts?: WorldContentCounts
  active: boolean
  manage: boolean
  canDelete: boolean
  onEnter: () => void
  onRename: (name: string) => void
  onDelete: () => void
}) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(world.name)

  useEffect(() => {
    setDraft(world.name)
  }, [world.name])

  const commit = useCallback(() => {
    const next = draft.trim()
    setEditing(false)
    if (next && next !== world.name) onRename(next)
  }, [draft, onRename, world.name])

  if (editing) {
    return (
      <div className="flex items-center gap-2 rounded-xl border border-line-hover bg-paper-card px-4 py-3">
        <input
          autoFocus
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          onBlur={commit}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault()
              commit()
            }
            if (event.key === 'Escape') {
              setEditing(false)
              setDraft(world.name)
            }
          }}
          className="h-9 flex-1 rounded-lg border border-line bg-paper px-3 text-sm text-ink outline-none focus:border-line-hover"
        />
        <button
          type="button"
          onMouseDown={(event) => event.preventDefault()}
          onClick={commit}
          className="flex h-9 w-9 items-center justify-center rounded-lg bg-ink text-paper"
          title="保存名称"
        >
          <Check size={16} strokeWidth={2} />
        </button>
      </div>
    )
  }

  return (
    <div
      className={`group flex items-center gap-3 rounded-xl border px-4 py-3 transition-colors ${
        active
          ? 'border-line-hover bg-paper-card'
          : 'border-line bg-paper-card/40 hover:border-line-hover'
      }`}
    >
      <button
        type="button"
        onClick={onEnter}
        className="flex min-w-0 flex-1 items-center gap-3 text-left"
      >
        <span
          className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg"
          style={{ backgroundColor: world.accentColor ?? 'var(--color-paper-card)' }}
        >
          <Globe2 size={18} strokeWidth={2} className="text-ink-muted" />
        </span>
        <span className="min-w-0 flex-1">
          <span className="flex items-center gap-2">
            <span className="truncate font-serif text-base text-ink">{world.name}</span>
            {active ? (
              <span className="shrink-0 rounded border border-line px-1.5 py-0.5 text-[10px] text-ink-muted">
                当前
              </span>
            ) : null}
          </span>
          {world.description ? (
            <span className="mt-0.5 block truncate text-xs text-ink-muted">{world.description}</span>
          ) : null}
          <span className="mt-1 block">
            <CountLine counts={counts} />
          </span>
        </span>
        <span className="shrink-0 text-[11px] text-ink-faint">{formatDate(world.createdAt)}</span>
      </button>
      {manage ? (
        <div className="flex shrink-0 items-center gap-1">
          <button
            type="button"
            onClick={() => setEditing(true)}
            className="flex h-8 w-8 items-center justify-center rounded-lg text-ink-faint transition-colors hover:bg-black/5 hover:text-ink dark:hover:bg-white/5"
            title="重命名"
          >
            <Pencil size={14} strokeWidth={2} />
          </button>
          <button
            type="button"
            onClick={onDelete}
            disabled={!canDelete}
            className="flex h-8 w-8 items-center justify-center rounded-lg text-ink-faint transition-colors hover:bg-error/10 hover:text-error disabled:cursor-not-allowed disabled:opacity-40"
            title={canDelete ? '删除世界观' : '至少保留一个世界观'}
          >
            <Trash2 size={14} strokeWidth={2} />
          </button>
        </div>
      ) : null}
    </div>
  )
}

export function WorldPicker({ mode = 'gate', onClose }: WorldPickerProps) {
  const {
    worlds,
    activeWorldId,
    selectWorld,
    createWorld,
    renameWorld,
    removeWorld,
  } = useWorlds()
  const [counts, setCounts] = useState<Record<number, WorldContentCounts>>({})
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [creating, setCreating] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [pendingDelete, setPendingDelete] = useState<World | null>(null)
  const [error, setError] = useState<string | null>(null)

  const worldIds = useMemo(
    () => worlds.map((world) => world.id).filter((id): id is number => id !== undefined),
    [worlds],
  )
  const worldIdsKey = worldIds.join(',')

  useEffect(() => {
    let cancelled = false
    void (async () => {
      const entries = await Promise.all(
        worldIds.map(async (id) => [id, await countWorldContents(id)] as const),
      )
      if (cancelled) return
      setCounts(Object.fromEntries(entries))
    })()
    return () => {
      cancelled = true
    }
    // worldIdsKey 覆盖列表内容变化，避免每次渲染重新统计。
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [worldIdsKey])

  const handleCreate = useCallback(
    async (event: FormEvent) => {
      event.preventDefault()
      const trimmed = name.trim()
      if (!trimmed) {
        setError('请先填写世界观名称')
        return
      }
      if (worlds.some((world) => world.name === trimmed)) {
        setError('已经存在同名世界观')
        return
      }
      setError(null)
      setCreating(true)
      try {
        const world = await createWorld({ name: trimmed, description })
        setName('')
        setDescription('')
        if (world.id !== undefined) {
          selectWorld(world.id)
          if (mode === 'manage') onClose?.()
        }
      } catch (cause) {
        setError(cause instanceof Error ? cause.message : '创建失败')
      } finally {
        setCreating(false)
      }
    },
    [createWorld, description, name, selectWorld, worlds],
  )

  const content = (
    <div className={mode === 'gate' ? 'flex min-h-screen w-full items-center justify-center bg-paper px-4 py-10' : ''}>
      <div
        className={
          mode === 'gate'
            ? 'w-full max-w-2xl rounded-2xl border border-line bg-paper p-6 shadow-sm'
            : 'w-[560px] max-w-[calc(100vw-32px)] rounded-2xl border border-line bg-paper p-5 shadow-xl'
        }
      >
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 className="font-serif text-xl text-ink">
              {mode === 'gate' ? '选择世界观' : '管理世界观'}
            </h2>
            <p className="mt-1 text-xs text-ink-muted">
              每个世界观拥有独立的角色、事件、国家、词条、时间线与关系图，互不混杂。
            </p>
          </div>
          {mode === 'manage' ? (
            <button
              type="button"
              onClick={onClose}
              className="flex h-8 w-8 items-center justify-center rounded-lg text-ink-faint hover:bg-black/5 hover:text-ink dark:hover:bg-white/5"
              title="关闭"
            >
              <X size={16} strokeWidth={2} />
            </button>
          ) : null}
        </div>

        <div className="mt-4 max-h-[46vh] space-y-2 overflow-y-auto pr-1">
          {worlds.length === 0 ? (
            <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-line px-4 py-8 text-center">
              <BookOpen size={22} strokeWidth={2} className="text-ink-faint" />
              <p className="mt-2 text-sm text-ink">还没有世界观</p>
              <p className="mt-1 text-xs text-ink-muted">
                新建一个世界观作为容器，之后所有内容都会保存在它里面。
              </p>
            </div>
          ) : (
            worlds.map((world) => (
              <WorldCard
                key={world.id}
                world={world}
                counts={world.id !== undefined ? counts[world.id] : undefined}
                active={world.id === activeWorldId}
                manage={mode === 'manage'}
                canDelete={worlds.length > 1}
                onEnter={() => {
                  if (world.id === undefined) return
                  selectWorld(world.id)
                  onClose?.()
                }}
                onRename={(next) => {
                  if (world.id === undefined) return
                  void renameWorld(world.id, next)
                }}
                onDelete={() => setPendingDelete(world)}
              />
            ))
          )}
        </div>

        <form onSubmit={(event) => void handleCreate(event)} className="mt-5 rounded-xl border border-line bg-paper-card/50 p-4">
          <p className="flex items-center gap-1.5 text-xs text-ink-muted">
            <Plus size={13} strokeWidth={2} />
            新建世界观
          </p>
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="世界观名称，例如：雾海纪元"
            className="mt-2 h-9 w-full rounded-lg border border-line bg-paper px-3 text-sm text-ink outline-none focus:border-line-hover"
          />
          <input
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            placeholder="一句话简介（可选）"
            className="mt-2 h-9 w-full rounded-lg border border-line bg-paper px-3 text-sm text-ink outline-none focus:border-line-hover"
          />
          {error ? <p className="mt-2 text-xs text-error">{error}</p> : null}
          <div className="mt-3 flex items-center justify-between">
            <span className="text-[11px] text-ink-faint">
              {mode === 'gate' && worlds.length > 0 ? '选择已有世界观或新建一个' : '内容完全隔离，切换时不会互相影响'}
            </span>
            <button
              type="submit"
              disabled={creating || !name.trim()}
              className="h-8 rounded-lg bg-ink px-3 text-xs text-paper transition-opacity hover:opacity-90 disabled:opacity-40"
            >
              {creating ? '创建中…' : '创建并进入'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )

  const confirm = (
    <AppConfirm
      open={pendingDelete !== null}
      busy={deleting}
      title="删除世界观"
      description={
        pendingDelete
          ? `确定删除「${pendingDelete.name}」？其中的角色、事件等内容会从列表中隐藏并保留在本地数据库中，不会立即物理删除。`
          : undefined
      }
      confirmLabel="删除"
      danger
      onCancel={() => {
        if (deleting) return
        setPendingDelete(null)
      }}
      onConfirm={() => {
        const target = pendingDelete
        if (!target?.id) return
        setDeleting(true)
        removeWorld(target.id)
          .then(() => {
            setPendingDelete(null)
            setError(null)
          })
          .catch((cause: unknown) => {
            setError(cause instanceof Error ? cause.message : '删除失败')
          })
          .finally(() => setDeleting(false))
      }}
    />
  )

  if (mode === 'manage') {
    return createPortal(
      <div
        className="fixed inset-0 z-[140] flex items-center justify-center bg-black/30 p-4 backdrop-blur-sm"
        onClick={(event) => {
          if (event.target === event.currentTarget) onClose?.()
        }}
      >
        {content}
        {confirm}
      </div>,
      document.getElementById('overlay-root') ?? document.body,
    )
  }

  return (
    <>
      {content}
      {confirm}
    </>
  )
}
