namespace ProjectAPI.Api.Application.Sales.GetSalesByUser;

public class GetSalesByUserQuery : IRequest<UserSalesResponse>
{
    public string UserId { get; set; }
}