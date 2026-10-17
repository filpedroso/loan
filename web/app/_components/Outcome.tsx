"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import type { PublicReason } from "@/lib/api";
import { stateName } from "@/lib/states";
import { CheckIcon, InfoIcon } from "./icons";

const SUPPORT_EMAIL = "support@example.com";

const primaryButton =
  "rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";
const secondaryButton =
  "rounded-lg bg-white px-4 py-2.5 text-sm font-semibold text-slate-900 shadow-xs ring-1 ring-inset ring-slate-300 " +
  "hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";

type ApprovedProps = { firstName: string; amount: string; applicationId: string; onRestart: () => void };

export function Approved({ firstName, amount, applicationId, onRestart }: ApprovedProps) {
  const [copied, setCopied] = useState(false);
  const copy = () =>
    navigator.clipboard
      ?.writeText(applicationId)
      .then(() => setCopied(true))
      .catch(() => setCopied(false));

  return (
    <div>
      <Header
        icon={<CheckIcon className="size-8 text-emerald-600" />}
        tone="bg-emerald-50 ring-emerald-200"
        title={`You’re approved, ${firstName}!`}
      >
        We’ve approved your application for <strong className="font-semibold text-slate-900">${amount}</strong>.
      </Header>

      <div className="mt-8 rounded-xl bg-slate-50 px-4 py-4 text-center ring-1 ring-slate-200">
        <p className="text-sm text-slate-500">Your reference number</p>
        <div className="mt-1 flex flex-wrap items-center justify-center gap-x-3 gap-y-1">
          <code className="font-mono text-sm break-all text-slate-900">{applicationId}</code>
          <button type="button" onClick={copy} className="text-sm font-medium text-indigo-700 hover:text-indigo-500">
            {copied ? "Copied" : "Copy"}
          </button>
        </div>
      </div>

      <h2 className="mt-8 text-xs font-semibold uppercase tracking-wider text-slate-500">What happens next</h2>
      <ol className="mt-3 space-y-3 text-slate-700">
        {[
          "We’re passing your application to our lending team.",
          "A loan specialist will contact you to confirm the details and the next steps.",
          "Keep your reference number handy in case you have questions.",
        ].map((step, index) => (
          <li key={step} className="flex gap-3">
            <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-indigo-50 text-xs font-semibold text-indigo-700 ring-1 ring-indigo-200">
              {index + 1}
            </span>
            {step}
          </li>
        ))}
      </ol>

      <p className="mt-8 text-sm text-slate-600">
        Questions? Write to us at <SupportLink />.
      </p>
      <div className="mt-8 flex justify-center">
        <button type="button" onClick={onRestart} className={secondaryButton}>
          Start a new application
        </button>
      </div>
    </div>
  );
}

type DeniedProps = { reason: PublicReason | null; state: string; onEdit: () => void; onRestart: () => void };

export function Denied({ reason, state, onEdit, onRestart }: DeniedProps) {
  return (
    <div>
      <Header
        icon={<InfoIcon className="size-8 text-amber-600" />}
        tone="bg-amber-50 ring-amber-200"
        title="We can’t approve this application right now"
      >
        Thank you for applying. Here’s what we found.
      </Header>

      <div className="mt-8 space-y-4 text-slate-700">
        {reason === "StateNotServed" && (
          <p>
            We’re not able to offer loans in {stateName(state)} at the moment. This is about where we operate, not about
            you.
          </p>
        )}
        {reason === "NotEligible" && (
          <p>
            Based on the information provided, we’re unable to approve this application. This isn’t a judgment of you or
            your business.
          </p>
        )}
        <p>
          If you think something isn’t right, write to us at <SupportLink /> and we’ll be glad to take a look.
        </p>
      </div>

      <div className="mt-8 flex flex-col-reverse gap-3 sm:flex-row sm:justify-center">
        <button type="button" onClick={onRestart} className={secondaryButton}>
          Start a new application
        </button>
        <button type="button" onClick={onEdit} className={primaryButton}>
          Edit application
        </button>
      </div>
    </div>
  );
}

function Header({ icon, tone, title, children }: { icon: ReactNode; tone: string; title: string; children: ReactNode }) {
  // Move focus to the result, so screen readers announce it.
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    heading.current?.focus();
  }, []);

  return (
    <div className="text-center">
      <div className={`mx-auto flex size-16 items-center justify-center rounded-full ring-1 ${tone}`}>{icon}</div>
      <h1 ref={heading} tabIndex={-1} className="mt-6 text-2xl font-semibold tracking-tight text-slate-900 outline-none">
        {title}
      </h1>
      <p className="mt-2 text-slate-600">{children}</p>
    </div>
  );
}

function SupportLink() {
  return (
    <a href={`mailto:${SUPPORT_EMAIL}`} className="font-medium text-indigo-700 hover:text-indigo-500">
      {SUPPORT_EMAIL}
    </a>
  );
}
