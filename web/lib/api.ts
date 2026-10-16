import { FIELDS, type FieldErrors, type FieldName } from "./application";

export type PublicReason = "StateNotServed" | "NotEligible";

export type Decision = {
  decision: "Approved" | "Denied";
  applicationId: string | null;
  reasons: PublicReason[];
};

export type SubmitResult =
  | { kind: "decision"; decision: Decision }
  | { kind: "invalid"; errors: FieldErrors }
  | { kind: "failed"; message: string };

// Inlined at build time (NEXT_PUBLIC_*). The browser calls the API directly; the API allows this origin via CORS.
const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";

const SERVER_ERROR = "Something went wrong on our side, and your application wasn't saved. Please try again.";

export async function submitApplication(payload: object): Promise<SubmitResult> {
  let response: Response;
  try {
    response = await fetch(`${API_URL}/api/v1/applications`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
  } catch {
    return { kind: "failed", message: "We couldn't reach our servers. Check your connection and try again." };
  }

  if (response.ok) return { kind: "decision", decision: (await response.json()) as Decision };
  if (response.status === 429)
    return { kind: "failed", message: "Too many attempts from your network. Please wait a minute and try again." };
  if (response.status === 400) {
    const errors = fieldErrors(await response.json().catch(() => null));
    if (Object.keys(errors).length > 0) return { kind: "invalid", errors };
  }
  return { kind: "failed", message: SERVER_ERROR };
}

/** Picks the first message per known field from a ValidationProblemDetails body. */
function fieldErrors(problem: unknown): FieldErrors {
  const errors: FieldErrors = {};
  const reported = (problem as { errors?: Record<string, string[]> } | null)?.errors ?? {};
  for (const [field, messages] of Object.entries(reported)) {
    if (FIELDS.includes(field as FieldName)) errors[field as FieldName] = messages[0];
  }
  return errors;
}
