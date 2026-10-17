import { afterEach, describe, expect, it, vi } from "vitest";
import { submitApplication } from "./api";

const SERVER_ERROR = { kind: "failed", message: expect.stringContaining("went wrong on our side") };

/** Makes fetch answer with the given status and body (a string is sent as is, anything else as JSON). */
function answer(status: number, body: unknown) {
  const fetch = vi.fn().mockResolvedValue(new Response(typeof body === "string" ? body : JSON.stringify(body), { status }));
  vi.stubGlobal("fetch", fetch);
  return fetch;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("submitApplication", () => {
  it("posts the payload as JSON to the API", async () => {
    const fetch = answer(200, { approved: true, applicationId: "id-1", reason: null });

    await submitApplication({ ssn: "123456789" });

    expect(fetch).toHaveBeenCalledWith("http://localhost:8080/api/v1/applications", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: '{"ssn":"123456789"}',
    });
  });

  it.each([
    { approved: true, applicationId: "id-1", reason: null },
    { approved: false, applicationId: null, reason: "StateNotServed" },
  ])("returns the decision from a 200: %j", async (decision) => {
    answer(200, decision);

    expect(await submitApplication({})).toEqual({ kind: "decision", decision });
  });

  it("reports a failure when a 200 body isn't JSON", async () => {
    answer(200, "<html>proxy page</html>");

    expect(await submitApplication({})).toEqual(SERVER_ERROR);
  });

  it("returns the first message of each known field from a 400", async () => {
    answer(400, { errors: { ssn: ["Must have 9 digits.", "Second message."], somethingElse: ["Ignored."] } });

    expect(await submitApplication({})).toEqual({ kind: "invalid", errors: { ssn: "Must have 9 digits." } });
  });

  it("reports a failure for a 400 that names no known field", async () => {
    answer(400, { errors: { somethingElse: ["Ignored."] } });

    expect(await submitApplication({})).toEqual(SERVER_ERROR);
  });

  it("reports a failure for a 500", async () => {
    answer(500, "");

    expect(await submitApplication({})).toEqual(SERVER_ERROR);
  });

  it("reports a connection problem when the API can't be reached", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    expect(await submitApplication({})).toEqual({ kind: "failed", message: expect.stringContaining("couldn't reach") });
  });
});
