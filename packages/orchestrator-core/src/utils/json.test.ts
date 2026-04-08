import { describe, expect, it } from "vitest";
import { extractJsonObject } from "./json";

describe("extractJsonObject", () => {
  it("parses raw JSON", () => {
    expect(extractJsonObject<{ hello: string }>('{ "hello": "world" }')).toEqual({
      hello: "world",
    });
  });

  it("parses fenced JSON", () => {
    expect(
      extractJsonObject<{ value: number }>("```json\n{\"value\":42}\n```"),
    ).toEqual({
      value: 42,
    });
  });
});
