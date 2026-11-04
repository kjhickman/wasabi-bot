import { tracing } from 'cloudflare:workers';
import type OpenAI from 'openai';
import type { ResponseCreateParamsNonStreaming } from 'openai/resources/responses/responses';

export const MODELS = {
  text: 'gpt-6.1-terra',
  decision: 'gpt-6-luna',
  image: 'gpt-image-2.5-flare',
} as const;

export function createResponse(
  client: OpenAI,
  body: ResponseCreateParamsNonStreaming,
  signal: AbortSignal,
) {
  return tracing.enterSpan('openai.response', async (span) => {
    const response = await client.responses.create(body, { signal });
    span.setAttributes({
      model: body.model,
      status: response.status,
      inputTokens: response.usage?.input_tokens,
      outputTokens: response.usage?.output_tokens,
      reasoningTokens: response.usage?.output_tokens_details.reasoning_tokens,
    });
    return response;
  });
}
