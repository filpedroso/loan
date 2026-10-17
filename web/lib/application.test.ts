import { describe, expect, it } from "vitest";
import { emptyForm, formatAmount, toPayload, validate, type FormValues } from "./application";

const form: FormValues = {
  firstName: "Jane",
  lastName: "Doe",
  companyName: "Acme Corp",
  address: "1 Main St, Springfield, IL 62701",
  state: "CA",
  requestedAmount: "50,000",
  ssn: "123-45-6789",
};

describe("validate", () => {
  it("accepts a complete form", () => {
    expect(validate(form)).toEqual({});
  });

  it("flags every field of an empty form", () => {
    expect(Object.keys(validate(emptyForm)).sort()).toEqual(Object.keys(emptyForm).sort());
  });

  it("flags text longer than its limit", () => {
    expect(validate({ ...form, firstName: "a".repeat(101) }).firstName).toBe("Keep it under 100 characters");
  });

  it("flags a state that isn't a US state code", () => {
    expect(validate({ ...form, state: "XX" }).state).toBe("Select a state");
  });

  it.each(["50000", "50,000", "50000.5", "1,500.50", " 7 "])("accepts the amount %j", (requestedAmount) => {
    expect(validate({ ...form, requestedAmount }).requestedAmount).toBeUndefined();
  });

  // A decimal comma ("1500,50") must fail, not turn into 150,050.
  it.each(["1500,50", "0,5", "50,0000", ",500", "10.001", "1e5", ""])("rejects the amount %j", (requestedAmount) => {
    expect(validate({ ...form, requestedAmount }).requestedAmount).toBe("Enter an amount like 50,000 or 50000.50");
  });

  it.each(["0", "0.00"])("rejects the zero amount %j", (requestedAmount) => {
    expect(validate({ ...form, requestedAmount }).requestedAmount).toBe("Enter an amount greater than $0");
  });

  it("rejects an amount with more than 16 whole digits", () => {
    expect(validate({ ...form, requestedAmount: "12345678901234567" }).requestedAmount).toBe("That amount is too large");
  });

  it.each(["123456789", "123-45-6789", " 123-45-6789 "])("accepts the SSN %j", (ssn) => {
    expect(validate({ ...form, ssn }).ssn).toBeUndefined();
  });

  it.each(["12345678", "1234567890", "123-456-789", "123-45-678a"])("rejects the SSN %j", (ssn) => {
    expect(validate({ ...form, ssn }).ssn).toBe("Enter your 9-digit SSN, like 123-45-6789");
  });
});

describe("toPayload", () => {
  it("trims the text, drops amount commas and SSN dashes, and sends the amount as a string", () => {
    expect(toPayload({ ...form, firstName: " Jane ", requestedAmount: "1,500.50" })).toEqual({
      firstName: "Jane",
      lastName: "Doe",
      companyName: "Acme Corp",
      address: "1 Main St, Springfield, IL 62701",
      state: "CA",
      requestedAmount: "1500.50",
      ssn: "123456789",
    });
  });
});

describe("formatAmount", () => {
  it.each([
    ["50000", "50,000"],
    ["1500.5", "1,500.50"],
    ["1,234,567.89", "1,234,567.89"],
    ["9999999999999999.99", "9,999,999,999,999,999.99"], // Beyond what a JavaScript number holds exactly.
  ])("shows %j as %j", (amount, shown) => {
    expect(formatAmount(amount)).toBe(shown);
  });
});
