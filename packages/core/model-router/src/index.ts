/**
 * Per-step model routing: a reasoning model for a turn's first step and an
 * execution model for its tool-continuation steps. Opt-in: inactive until the
 * user-owned `agent-model-router` settings section names both slots.
 *
 * @module @deepseek-ai/dsh-model-router
 */

import { Context, Service } from '@deepseek-ai/cordis'
import z from '@deepseek-ai/schemastery'
import type { ModelSelection } from '@deepseek-ai/dsh-agent'
import { ReasoningEffortId } from '@deepseek-ai/dsh-llm'
import { installSettingsSection, settingsNamespace } from '@deepseek-ai/dsh-settings'

declare module '@deepseek-ai/cordis' {
  interface Context {
    /** Per-step model router: reasoning model on a turn's first step, execution model after. */
    modelRouter: ModelRouter
  }
}

/** Settings namespace carrying the per-step reasoning/execution slots. */
export const MODEL_ROUTER_SETTINGS_NAMESPACE = settingsNamespace('agent-model-router')

/** One router slot: a provider/model pair plus an optional reasoning effort. */
export interface ModelRouterSlot {
  /** Registered provider route. */
  provider: string
  /** Provider-owned model id. */
  model: string
  /** Adapter-owned reasoning effort, or provider/default behavior when absent. */
  reasoningEffort?: string
}

/** The two slots the router selects between; either may be absent, which disables routing. */
export interface ModelRouterSettings {
  /** Serves a turn's first step — the fresh reasoning about user intent. */
  reasoning?: ModelRouterSlot
  /** Serves the tool-continuation steps that execute the decided work. */
  execution?: ModelRouterSlot
}

const SLOT_SCHEMA: z<ModelRouterSlot> = z.object({
  provider: z.string().required(),
  model: z.string().required(),
  reasoningEffort: z.string(),
})

/** Schema of the agent-model-router settings section. */
export const MODEL_ROUTER_SETTINGS_SCHEMA: z<ModelRouterSettings> = z.object({
  reasoning: SLOT_SCHEMA,
  execution: SLOT_SCHEMA,
})

/** Composition entry: optional deployment defaults for both slots. */
export interface Config extends ModelRouterSettings {}

/** Project a slot onto the Agent-facing selection type. */
function toSelection(slot: ModelRouterSlot): ModelSelection {
  return {
    provider: slot.provider,
    model: slot.model,
    ...slot.reasoningEffort === undefined
      ? {}
      : { reasoningEffort: ReasoningEffortId(slot.reasoningEffort) },
  }
}

/**
 * Owns the per-step model routing policy independently of any transport. The
 * composition entry remains usable without a settings provider; when one is
 * mounted, its user layer is read live, so an edit to `agent-model-router`
 * takes effect on the next step without a restart.
 */
export class ModelRouter extends Service {
  static Config: z<Config> = z.object({
    reasoning: SLOT_SCHEMA,
    execution: SLOT_SCHEMA,
  })

  private source: () => ModelRouterSettings

  constructor(ctx: Context, config: Config) {
    super(ctx, 'modelRouter')
    const entry: ModelRouterSettings = {
      ...config.reasoning === undefined ? {} : { reasoning: config.reasoning },
      ...config.execution === undefined ? {} : { execution: config.execution },
    }
    this.source = () => entry
    installSettingsSection(ctx, MODEL_ROUTER_SETTINGS_NAMESPACE, MODEL_ROUTER_SETTINGS_SCHEMA, entry, {
      setSource: (current) => { this.source = current },
      onChange: () => {},
    })
  }

  /**
   * The model slot one request uses. In plan mode (Cline-style Plan) the
   * reasoning slot serves every step; otherwise the execution slot serves
   * every step. When plan state is unknown (either mode omitted), fall back to
   * the step rule: a turn's first step reasons, later tool-continuation steps
   * execute.
   * @param payload.planActive - whether plan mode is currently on; `undefined` when unknown.
   * @param payload.step - 1-based step number within the current turn.
   * @returns the selected slot, or `undefined` when either slot is unset.
   */
  route(payload: { planActive?: boolean; step: number }): ModelSelection | undefined {
    const { reasoning, execution } = this.source()
    if (reasoning === undefined || execution === undefined) return undefined
    if (payload.planActive !== undefined) return toSelection(payload.planActive ? reasoning : execution)
    return toSelection(payload.step <= 1 ? reasoning : execution)
  }
}

export default ModelRouter
