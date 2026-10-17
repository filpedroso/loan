"use client";

import type { ReactNode } from "react";
import type { FieldErrors, FieldName, FormValues } from "@/lib/application";
import { US_STATES } from "@/lib/states";
import { LockIcon } from "./icons";

// Horizontal padding is set per input, so the $ prefix has room.
const inputBase =
  "block w-full rounded-lg bg-white py-2.5 text-slate-900 shadow-xs ring-1 ring-inset ring-slate-300 outline-none " +
  "placeholder:text-slate-400 focus:ring-2 focus:ring-indigo-600 aria-invalid:ring-2 aria-invalid:ring-red-600";

type Props = {
  values: FormValues;
  errors: FieldErrors;
  formError?: string;
  onChange: (field: FieldName, value: string) => void;
  onSubmit: () => void;
};

export function ApplicationForm({ values, errors, formError, onChange, onSubmit }: Props) {
  // What every input shares: identity, value and the accessibility wiring to its hint or error.
  const bind = (field: FieldName, hasHint = false) => ({
    id: field,
    name: field,
    value: values[field],
    "aria-invalid": errors[field] ? true : undefined,
    "aria-describedby": errors[field] || hasHint ? `${field}-note` : undefined,
  });

  // POST, so a submit before the page finishes loading can't put the SSN in the URL.
  return (
    <form
      method="post"
      noValidate
      className="space-y-8"
      onSubmit={(event) => {
        event.preventDefault();
        onSubmit();
      }}
    >
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-slate-900">Apply for a business loan</h1>
        <p className="mt-2 text-slate-600">It takes about two minutes, and you’ll get a decision right away.</p>
      </div>

      {formError && (
        <div role="alert" className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800 ring-1 ring-red-200">
          {formError}
        </div>
      )}

      <Section title="About you">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field field="firstName" label="First name" error={errors.firstName}>
            <input
              {...bind("firstName")}
              className={`${inputBase} px-3.5`}
              autoComplete="given-name"
              onChange={(e) => onChange("firstName", e.target.value)}
            />
          </Field>
          <Field field="lastName" label="Last name" error={errors.lastName}>
            <input
              {...bind("lastName")}
              className={`${inputBase} px-3.5`}
              autoComplete="family-name"
              onChange={(e) => onChange("lastName", e.target.value)}
            />
          </Field>
        </div>
      </Section>

      <Section title="Your business">
        <Field field="companyName" label="Company name" error={errors.companyName}>
          <input
            {...bind("companyName")}
            className={`${inputBase} px-3.5`}
            autoComplete="organization"
            onChange={(e) => onChange("companyName", e.target.value)}
          />
        </Field>
        <Field field="address" label="Business address" hint="Street, city and ZIP code" error={errors.address}>
          <input
            {...bind("address", true)}
            className={`${inputBase} px-3.5`}
            autoComplete="street-address"
            placeholder="123 Main St, Springfield, IL 62701"
            onChange={(e) => onChange("address", e.target.value)}
          />
        </Field>
        <Field field="state" label="State" error={errors.state}>
          <select
            {...bind("state")}
            className={`${inputBase} px-3`}
            autoComplete="address-level1"
            onChange={(e) => onChange("state", e.target.value)}
          >
            <option value="">Select a state</option>
            {US_STATES.map((state) => (
              <option key={state.code} value={state.code}>
                {state.name}
              </option>
            ))}
          </select>
        </Field>
      </Section>

      <Section title="Your loan">
        <Field field="requestedAmount" label="Amount you’d like to borrow" error={errors.requestedAmount}>
          <span className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-slate-500">$</span>
          <input
            {...bind("requestedAmount")}
            className={`${inputBase} pl-7 pr-3.5`}
            inputMode="decimal"
            autoComplete="off"
            placeholder="50,000"
            onChange={(e) => onChange("requestedAmount", e.target.value)}
          />
        </Field>
      </Section>

      <Section title="Identity verification">
        <Field
          field="ssn"
          label="Social Security number"
          error={errors.ssn}
          hint={
            <>
              <LockIcon className="size-4 shrink-0" />
              Encrypted at rest and used only to process this application.
            </>
          }
        >
          <input
            {...bind("ssn", true)}
            className={`${inputBase} px-3.5 tracking-wider`}
            inputMode="numeric"
            autoComplete="off"
            spellCheck={false}
            maxLength={11}
            placeholder="123-45-6789"
            onChange={(e) => onChange("ssn", e.target.value)}
          />
        </Field>
      </Section>

      <div className="space-y-3">
        <button
          type="submit"
          className="w-full rounded-lg bg-indigo-600 px-4 py-3 text-base font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
        >
          Submit application
        </button>
        <p className="text-center text-xs text-slate-500">By submitting, you confirm this information is accurate.</p>
      </div>
    </form>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <fieldset>
      <legend className="mb-4 text-xs font-semibold uppercase tracking-wider text-slate-500">{title}</legend>
      <div className="space-y-4">{children}</div>
    </fieldset>
  );
}

function Field(props: { field: FieldName; label: string; hint?: ReactNode; error?: string; children: ReactNode }) {
  const { field, label, hint, error, children } = props;
  return (
    <div>
      <label htmlFor={field} className="block text-sm font-medium text-slate-800">
        {label}
      </label>
      <div className="relative mt-1.5">{children}</div>
      {error ? (
        <p id={`${field}-note`} className="mt-1.5 text-sm text-red-700">
          {error}
        </p>
      ) : hint ? (
        <p id={`${field}-note`} className="mt-1.5 flex items-center gap-1.5 text-sm text-slate-500">
          {hint}
        </p>
      ) : null}
    </div>
  );
}
