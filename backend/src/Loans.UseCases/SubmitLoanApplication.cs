using Loans.Domain;
using Loans.Domain.Customers;
using Loans.Domain.Decisions;

namespace Loans.UseCases;

/// <summary>The use case: decide on a loan application and, if approved, save it and schedule the CRM sync.</summary>
public sealed class SubmitLoanApplication(ICustomerRepository customers, DecisionEngine engine)
{
    public async Task<SubmissionResult> HandleAsync(SubmitLoanApplicationCommand command, CancellationToken ct)
    {
        var ssn = Ssn.Parse(command.Ssn);
        var amount = Money.Of(command.RequestedAmount, nameof(command.RequestedAmount));

        var customer = await customers.FindBySsnAsync(ssn, ct);
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

        var application = customer.Apply(amount);

        var decision = await engine.DecideAsync(customer, ct);
        if (!decision.Approved)
            return SubmissionResult.Denied(decision.Reasons); // Nothing is saved.

        await customers.SaveApprovedAsync(customer, ct);
        return SubmissionResult.Approved(application.Id);
    }
}
