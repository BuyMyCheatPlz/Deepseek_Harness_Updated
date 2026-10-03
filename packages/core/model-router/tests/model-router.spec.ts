import { describe, expect, it } from 'vitest'
import { Context } from '@deepseek-ai/cordis'
import { ReasoningEffortId } from '@deepseek-ai/dsh-llm'
import { SettingsProvider, type SettingsNamespace } from '@deepseek-ai/dsh-settings'
import { MODEL_ROUTER_SETTINGS_NAMESPACE, ModelRouter } from '../src/index.ts'

describe('ModelRouter: Plan / Act routing', () => {
  it('returns undefined when either reasoning or execution slot is missing', async () => {
    const ctx = new Context()
    await ctx.plugin(ModelRouter, {
      reasoning: { provider: 'deepseek-official', model: 'deepseek-reasoner' },
    })

    expect(ctx.modelRouter.route({ planActive: true, step: 1 })).toBeUndefined()
    expect(ctx.modelRouter.route({ planActive: false, step: 1 })).toBeUndefined()
    expect(ctx.modelRouter.route({ step: 1 })).toBeUndefined()
  })

  it('routes to reasoning slot in plan mode and execution slot in act mode', async () => {
    const ctx = new Context()
    await ctx.plugin(ModelRouter, {
      reasoning: { provider: 'deepseek-official', model: 'deepseek-reasoner', reasoningEffort: 'high' },
      execution: { provider: 'deepseek-official', model: 'deepseek-chat' },
    })

    // Plan mode active: all steps use reasoning model
    expect(ctx.modelRouter.route({ planActive: true, step: 1 })).toEqual({
      provider: 'deepseek-official',
      model: 'deepseek-reasoner',
      reasoningEffort: ReasoningEffortId('high'),
    })
    expect(ctx.modelRouter.route({ planActive: true, step: 2 })).toEqual({
      provider: 'deepseek-official',
      model: 'deepseek-reasoner',
      reasoningEffort: ReasoningEffortId('high'),
    })

    // Act mode (planActive false): all steps use execution model
    expect(ctx.modelRouter.route({ planActive: false, step: 1 })).toEqual({
      provider: 'deepseek-official',
      model: 'deepseek-chat',
    })
    expect(ctx.modelRouter.route({ planActive: false, step: 2 })).toEqual({
      provider: 'deepseek-official',
      model: 'deepseek-chat',
    })
  })

  it('falls back to step heuristic when planActive is undefined', async () => {
    const ctx = new Context()
    await ctx.plugin(ModelRouter, {
      reasoning: { provider: 'deepseek-official', model: 'deepseek-reasoner' },
      execution: { provider: 'deepseek-official', model: 'deepseek-chat' },
    })

    // Step 1: reasoning
    expect(ctx.modelRouter.route({ step: 1 })).toEqual({
      provider: 'deepseek-official',
      model: 'deepseek-reasoner',
    })

    // Step 2+: execution
    expect(ctx.modelRouter.route({ step: 2 })).toEqual({
      provider: 'deepseek-official',
      model: 'deepseek-chat',
    })
  })

  it('updates routing live when settings are modified', async () => {
    class MemorySettings extends SettingsProvider {
      doc: Record<string, unknown> = {}

      get writable(): boolean {
        return true
      }

      protected load(): Promise<Record<string, unknown>> {
        return Promise.resolve(structuredClone(this.doc))
      }

      protected persist(ns: SettingsNamespace, section: Record<string, unknown>): Promise<void> {
        this.doc = { ...this.doc, [ns]: structuredClone(section) }
        return Promise.resolve()
      }
    }

    const ctx = new Context()
    await ctx.plugin(MemorySettings)
    await ctx.plugin(ModelRouter, {})

    // Initially unconfigured
    expect(ctx.modelRouter.route({ planActive: true, step: 1 })).toBeUndefined()

    // Configure via settings
    await ctx.settings.update(MODEL_ROUTER_SETTINGS_NAMESPACE, {
      reasoning: { provider: 'deepseek-official', model: 'deepseek-reasoner' },
      execution: { provider: 'deepseek-official', model: 'deepseek-chat' },
    })

    expect(ctx.modelRouter.route({ planActive: true, step: 1 })).toEqual({
      provider: 'deepseek-official',
      model: 'deepseek-reasoner',
    })
    expect(ctx.modelRouter.route({ planActive: false, step: 1 })).toEqual({
      provider: 'deepseek-official',
      model: 'deepseek-chat',
    })
  })
})
