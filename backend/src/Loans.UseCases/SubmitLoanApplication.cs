using Loans.Domain.Customers;
using Loans.Domain.Decisions;
using Microsoft.Extensions.Logging;

namespace Loans.UseCases;

/// <summary>The use case: decide on a loan application and, if approved, save it and schedule the external partner sync.</summary>
public sealed class SubmitLoanApplication(
    ICustomerRepository customers, DecisionEngine engine, ILogger<SubmitLoanApplication> logger)
{
    public async Task<SubmissionResult> HandleAsync(SubmitLoanApplicationCommand command, CancellationToken ct)
    {
        var ssn = Ssn.Parse(command.Ssn);
        var amount = RequestedAmount.Of(command.RequestedAmount);

        var customer = await customers.LockAndFindBySsnAsync(ssn, ct);
        if (customer is null)
        {
            customer = Customer.Register(
                ssn, command.FirstName, command.LastName, command.Address, command.State, command.CompanyName);
        }
        else
        {
            customer.UpdateDetails(
                command.FirstName, command.LastName, command.Address, command.State, command.CompanyName);
        }

        var loanApplication = customer.Apply(amount);

        var decision = await engine.DecideAsync(customer, ct);
        // Logs never include the SSN, name or address.
        if (decision.Reason is { } reason)
        {
            logger.LogInformation("Denied a loan application from {State}: {Reason}.", customer.State, reason);
            return SubmissionResult.Denied(reason); // Nothing is saved, so nothing reaches the partner.
        }

        await customers.SaveAsync(customer, ct);
        logger.LogInformation("Approved loan application {LoanApplicationId}.", loanApplication.Id);
        return SubmissionResult.Approved(loanApplication.Id);
    }
}
