const MAX_INPUT_LENGTH = 200;

type Fields = { error?: unknown } & Record<string, unknown>;

// Workers Logs indexes the fields of a logged object, so each log is one object. Logs
// written inside a span are linked to it in the trace.
export const log = {
  info: (message: string, fields?: Fields) => console.log(entry(message, fields)),
  warn: (message: string, fields?: Fields) => console.warn(entry(message, fields)),
  error: (message: string, fields?: Fields) => console.error(entry(message, fields)),
};

// Shortens what someone typed, for logs of failures it might explain.
export function preview(text: string) {
  return text.length > MAX_INPUT_LENGTH ? `${text.slice(0, MAX_INPUT_LENGTH)}…` : text;
}

function entry(message: string, { error, ...fields }: Fields = {}) {
  return { message, ...fields, ...(error !== undefined && { error: describe(error) }) };
}

// Errors don't serialize to JSON, so copy out what helps diagnose one, including the
// status and request ID on OpenAI's errors.
function describe(error: unknown): unknown {
  if (!(error instanceof Error)) return error;
  const { status, code, requestID } = error as {
    status?: unknown;
    code?: unknown;
    requestID?: unknown;
  };
  return {
    name: error.name,
    message: error.message,
    status,
    code,
    requestID,
    stack: error.stack,
    ...(error.cause !== undefined && { cause: describe(error.cause) }),
  };
}
