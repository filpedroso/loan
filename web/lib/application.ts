import { US_STATES } from "./states";

export type FieldName =
  | "firstName"
  | "lastName"
  | "companyName"
  | "address"
  | "state"
  | "requestedAmount"
  | "ssn";

export type FormValues = Record<FieldName, string>;
export type FieldErrors = Partial<Record<FieldName, string>>;

/** Form order, used to focus the first invalid field. */
export const FIELDS: FieldName[] = ["firstName", "lastName", "companyName", "address", "state", "requestedAmount", "ssn"];

export const emptyForm: FormValues = {
  firstName: "",
  lastName: "",
  companyName: "",
  address: "",
  state: "",
  requestedAmount: "",
  ssn: "",
};

export const ssnDigits = (value: string) => value.replace(/\D/g, "").slice(0, 9);

/** 123456789 -> 123-45-6789, also while typing. */
export function formatSsn(value: string) {
  const digits = ssnDigits(value);
  if (digits.length <= 3) return digits;
  if (digits.length <= 5) return `${digits.slice(0, 3)}-${digits.slice(3)}`;
  return `${digits.slice(0, 3)}-${digits.slice(3, 5)}-${digits.slice(5)}`;
}

/** What the amount input accepts while typing: digits, commas and a dot. */
export const amountInput = (value: string) => value.replace(/[^\d.,]/g, "");

const plainAmount = (value: string) => value.replace(/,/g, "");
const validAmount = /^\d+(\.\d{1,2})?$/;

/** 1500.5 -> 1,500.50. Uses BigInt so very large amounts keep every digit. */
export function formatAmount(value: string) {
  const amount = plainAmount(value);
  if (!validAmount.test(amount)) return value;
  const [whole, cents] = amount.split(".");
  const grouped = BigInt(whole).toLocaleString("en-US");
  return cents === undefined ? grouped : `${grouped}.${cents.padEnd(2, "0")}`;
}

export function validate(values: FormValues): FieldErrors {
  const errors: FieldErrors = {};
  const required = (field: FieldName, maxLength: number, message: string) => {
    const value = values[field].trim();
    if (!value) errors[field] = message;
    else if (value.length > maxLength) errors[field] = `Keep it under ${maxLength} characters`;
  };

  required("firstName", 100, "Enter your first name");
  required("lastName", 100, "Enter your last name");
  required("companyName", 200, "Enter your company's name");
  required("address", 300, "Enter your business address");
  if (!US_STATES.some((state) => state.code === values.state)) errors.state = "Select a state";

  const amount = plainAmount(values.requestedAmount);
  if (!/^\d+(\.\d+)?$/.test(amount) || Number(amount) <= 0) errors.requestedAmount = "Enter an amount greater than $0";
  else if (!validAmount.test(amount)) errors.requestedAmount = "Use at most 2 decimal places";
  else if (amount.split(".")[0].replace(/^0+/, "").length > 16) errors.requestedAmount = "That amount is too large";

  if (ssnDigits(values.ssn).length !== 9) errors.ssn = "Enter all 9 digits of your SSN";
  return errors;
}

/** The JSON the API expects. The amount goes as a string so no digit is lost to floating point. */
export function toPayload(values: FormValues) {
  return {
    firstName: values.firstName.trim(),
    lastName: values.lastName.trim(),
    companyName: values.companyName.trim(),
    address: values.address.trim(),
    state: values.state,
    requestedAmount: plainAmount(values.requestedAmount),
    ssn: ssnDigits(values.ssn),
  };
}
