class FakeSpan {
  name: string;
  attributes: Record<string, string | number | boolean> = {};

  constructor(name: string) {
    this.name = name;
  }

  setAttribute(key: string, value: string | number | boolean | undefined) {
    if (value !== undefined) this.attributes[key] = value;
    return this;
  }

  setAttributes(attributes: Record<string, string | number | boolean | undefined>) {
    for (const [key, value] of Object.entries(attributes)) this.setAttribute(key, value);
    return this;
  }
}

export const spans: FakeSpan[] = [];

export const tracing = {
  enterSpan<T>(name: string, callback: (span: FakeSpan) => T): T {
    const span = new FakeSpan(name);
    spans.push(span);
    return callback(span);
  },
};
