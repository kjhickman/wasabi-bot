const MAX_INPUT_LENGTH = 200;

type Fields = { error?: unknown } & Record<string, unknown>;

// Workers Logs indexes the fields of a logged object, so each log is one object.
export const log = {
  info: (message: string, fields?: Fields) => console.log(entry(message, fields)),
  warn: (message: string, fields?: Fields) => console.warn(entry(message, fields)),
  error: (message: string, fields?: Fields) => console.error(entry(message, fields)),
};

export function preview(text: string) {
  return text.length > MAX_INPUT_LENGTH ? `${text.slice(0, MAX_INPUT_LENGTH)}…` : text;
}

function entry(message: string, { error, ...fields }: Fields = {}) {
  return { message, ...fields, ...(error !== undefined && { error: describe(error) }) };
}

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
