using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Quartiers.GetQuartiers
{
    /// <summary>
    /// Handler to retrieve a list of quartiers with optional filters.
    /// </summary>
    public class GetQuartiersHandler : IRequestHandler<GetQuartiersQuery, PaginatedResponse<QuartierListItem>>
    {
        private readonly IQuartierRepository _quartierRepository;

        public GetQuartiersHandler(IQuartierRepository quartierRepository)
        {
            _quartierRepository = quartierRepository;
        }

        public async Task<PaginatedResponse<QuartierListItem>> Handle(GetQuartiersQuery request, CancellationToken cancellationToken)
        {
            // 1. Retrieve all quartiers
            var allQuartiers = await _quartierRepository.GetAllAsync();

            // 2. Apply filters
            if (!string.IsNullOrEmpty(request.Name))
            {
                allQuartiers = allQuartiers.Where(q => q.Name.Contains(request.Name, StringComparison.OrdinalIgnoreCase));
            }

            var totalItems = allQuartiers.Count();

            // 3. Apply pagination
            var paginatedData = allQuartiers
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(q => new QuartierListItem
                {
                    Id = q.Id,
                    Name = q.Name
                })
                .ToList();

            // 4. Return paginated response
            return new PaginatedResponse<QuartierListItem>(paginatedData, request.PageNumber, request.PageSize, totalItems);
        }
    }
}
