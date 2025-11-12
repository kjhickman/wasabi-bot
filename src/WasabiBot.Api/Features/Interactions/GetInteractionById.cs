using WasabiBot.DataAccess.Abstractions;

namespace WasabiBot.Api.Features.Interactions;

public static class GetInteractionById
{
    public static async Task<IResult> Handle(long id, IInteractionService interactionService)
    {
        if (id <= 0)
        {
            return Results.BadRequest("Invalid interaction ID.");
        }
        var entity = await interactionService.GetByIdAsync(id);
        if (entity is null)
        {
            return Results.NotFound();
        }

        var dto = InteractionDto.FromEntity(entity);
        return Results.Ok(dto);
    }
}
