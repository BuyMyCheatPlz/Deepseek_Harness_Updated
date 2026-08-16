import { useEffect, useRef, useState } from 'react'
import type { InjectFace, PropsLocale, PropsRuntime } from '@deepseek-ai/dsh-client-ui-slots'
import { IconCloseFill14 } from '@deepseek-ai/dsh-client-ui-primitives'
// Type-only: pulls the ui-conversation SlotMap merge (the input.plan seat and
// its {locked} owner share).
import type {} from '@deepseek-ai/dsh-client-ui-conversation/client'
import type { PlanChipInjected } from './index.ts'
import css from './PlanModeControl.module.css'

/** Full plan-seat component props: runtime share (standard kit + locked owner prop) & injected share & the locale seat. */
export type PlanChipProps =
  PropsRuntime<'conversation.input.plan'> & InjectFace<PlanChipInjected> & PropsLocale<'plan'>

/**
 * Plan/Act toggle over the host-computed `plan` projection. Always visible:
 * while plan mode is off the chip reads "Act" and entering it runs /plan;
 * while plan mode is on the chip reads "Plan" and leaving runs /plan off.
 * Reads the effective target (`pending ? !active : active` — a folded host
 * value, not client optimism).
 */
export function PlanChip({ useProjection, locked, exitPlanMode, enterPlanMode, t }: PlanChipProps) {
  const plan = useProjection('plan')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const aliveRef = useRef(true)

  useEffect(() => {
    aliveRef.current = true
    return () => {
      aliveRef.current = false
    }
  }, [])

  if (plan === undefined) return null
  const active = plan.pending ? !plan.active : plan.active

  const toggle = (): void => {
    setBusy(true)
    setError(null)
    const run = active ? exitPlanMode : enterPlanMode
    void run().then((failure) => {
      if (!aliveRef.current) return
      setBusy(false)
      setError(failure)
    }, (reason: unknown) => {
      if (!aliveRef.current) return
      setBusy(false)
      setError(reason instanceof Error ? reason.message : String(reason))
    })
  }

  return (
    <span className={css.wrap}>
      <button
        type="button"
        className={active ? css.chipOn : css.chipOff}
        aria-label={active ? t('chip.on.aria') : t('chip.off.aria')}
        title={active ? t('chip.on.title') : t('chip.off.title')}
        disabled={locked || busy}
        onClick={toggle}
      >
        {active ? 'Plan' : 'Act'}
        {active && (
          <span className={css.close} aria-hidden>
            <IconCloseFill14 size={12} />
          </span>
        )}
      </button>
      {error !== null && <span className={css.error} role="status" title={error}>{error}</span>}
    </span>
  )
}
