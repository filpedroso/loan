using Loans.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Loans.Api;

[ApiController]
[Route("api/v1/applications")]
public sealed class LoanApplicationsController(SubmitLoanApplication submit) : ControllerBase
{
    /// <summary>A denial is a successful decision, so both outcomes are 200. Invalid input is 400.</summary>
    [HttpPost]
    public async Task<SubmitLoanApplicationResponse> Submit(SubmitLoanApplicationRequest request, CancellationToken ct) =>
        SubmitLoanApplicationResponse.From(await submit.HandleAsync(request.ToCommand(), ct));
}
