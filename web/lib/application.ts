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

/** 123-45-6789 or 123456789. */
const validSsn = /^\d{3}-?\d{2}-?\d{4}$/;

/** 50000, 50,000, 50000.5 or 50,000.50: commas only between groups of three digits, at most 2 decimals. */
const validAmount = /^(\d{1,3}(,\d{3})+|\d+)(\.\d{1,2})?$/;

const plainAmount = (value: string) => value.trim().replace(/,/g, "");

/** 1500.5 -> 1,500.50, for showing a valid amount. Uses BigInt so very large amounts keep every digit. */
export function formatAmount(value: string) {
  const [whole, cents] = plainAmount(value).split(".");
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

  const amount = values.requestedAmount.trim();
  if (!validAmount.test(amount)) errors.requestedAmount = "Enter an amount like 50,000 or 50000.50";
  else if (Number(plainAmount(amount)) <= 0) errors.requestedAmount = "Enter an amount greater than $0";
  else if (plainAmount(amount).split(".")[0].replace(/^0+/, "").length > 16)
    errors.requestedAmount = "That amount is too large";

  if (!validSsn.test(values.ssn.trim())) errors.ssn = "Enter your 9-digit SSN, like 123-45-6789";
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
    ssn: values.ssn.replace(/\D/g, ""),
  };
}
