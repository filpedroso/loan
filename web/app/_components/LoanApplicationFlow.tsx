"use client";

import { useState } from "react";
import { submitApplication, type PublicReason } from "@/lib/api";
import {
  emptyForm,
  FIELDS,
  formatAmount,
  toPayload,
  validate,
  type FieldErrors,
  type FieldName,
} from "@/lib/application";
import { ApplicationForm } from "./ApplicationForm";
import { Approved, Denied } from "./Outcome";
import { Reviewing } from "./Reviewing";

// The decision takes milliseconds. Keeping the handshake up a little longer reads as "reviewed", not as a flicker.
const MIN_REVIEW_MS = 1200;

type View =
  | { name: "form"; formError?: string }
  | { name: "reviewing" }
  | { name: "approved"; applicationId: string }
  | { name: "denied"; reason: PublicReason | null };

/** One page, three views: form, handshake while the API decides, then the result. Nothing is kept in the browser. */
export function LoanApplicationFlow() {
  const [values, setValues] = useState(emptyForm);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [view, setView] = useState<View>({ name: "form" });

  const change = (field: FieldName, value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  };

  const showErrors = (found: FieldErrors) => {
    setErrors(found);
    const first = FIELDS.find((field) => found[field]);
    if (first) requestAnimationFrame(() => document.getElementById(first)?.focus()); // After the form renders.
  };

  const submit = async () => {
    const found = validate(values);
    if (Object.keys(found).length > 0) {
      showErrors(found);
      return;
    }

    setView({ name: "reviewing" });
    const [result] = await Promise.all([submitApplication(toPayload(values)), delay(MIN_REVIEW_MS)]);

    if (result.kind === "decision") {
      const { approved, applicationId, reason } = result.decision;
      setView(approved && applicationId ? { name: "approved", applicationId } : { name: "denied", reason });
    } else if (result.kind === "invalid") {
      setView({ name: "form" });
      showErrors(result.errors);
    } else {
      setView({ name: "form", formError: result.message });
    }
  };

  const restart = () => {
    setValues(emptyForm);
    setErrors({});
    setView({ name: "form" });
  };

  switch (view.name) {
    case "reviewing":
      return <Reviewing />;
    case "approved":
      return (
        <Approved
          firstName={values.firstName.trim()}
          amount={formatAmount(values.requestedAmount)}
          applicationId={view.applicationId}
          onRestart={restart}
        />
      );
    case "denied":
      return (
        <Denied
          reason={view.reason}
          state={values.state}
          onEdit={() => setView({ name: "form" })}
          onRestart={restart}
        />
      );
    default:
      return (
        <ApplicationForm
          values={values}
          errors={errors}
          formError={view.formError}
          onChange={change}
          onSubmit={submit}
        />
      );
  }
}

const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));
