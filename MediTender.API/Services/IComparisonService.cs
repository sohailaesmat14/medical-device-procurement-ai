using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediTender.API.Models;

namespace MediTender.API.Services
{
    public interface IComparisonService
    {
        Task<List<OfferEvaluation>> CompareVendorsAsync(int tenderId, int userId, List<Standard> requirements, List<string> vendorNames, CancellationToken cancellationToken = default);
    }
}