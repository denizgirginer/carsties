using AuctionService.Data;
using AuctionService.DTOs;
using AuctionService.Entities;
using AutoMapper;
using Contracts;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionService.Controllers
{
    [ApiController]
    [Route("api/auctions")]
    public class AuctionsController: ControllerBase
    {
        private AuctionDbContext _context;
        private IMapper _mapper;
        private IAuctionRepository _repo;
        private readonly IPublishEndpoint _publishEndpoint;
        public AuctionsController(IMapper mapper, AuctionDbContext context, IAuctionRepository repo, IPublishEndpoint publishEndpoint)
        {
            _mapper = mapper;
            _context = context;
            _repo = repo;
            _publishEndpoint = publishEndpoint;
        }

        [HttpGet]
        public async Task<ActionResult<List<AuctionDto>>> GetAllAuctions([FromQuery] string? date)
        {
            var auctions = await _repo.GetAuctionsAsync(date);

            return Ok(auctions);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AuctionDto>> GetAuctionById(Guid id)
        {
            var auction = await _context.Auctions
                .Include(x => x.Item)
                .Where(x=>x.Id==id)
                .OrderBy(x => x.Item.Make)
                .SingleOrDefaultAsync();

            if (auction == null)
                return NotFound();

            return Ok(_mapper.Map<AuctionDto>(auction));
        }

        //[Authorize]
        [HttpPost]
        public async Task<ActionResult<AuctionDto>> CreateAuction(CreateAuctionDto auctionDto)
        {
            var auction = _mapper.Map<Auction>(auctionDto);

            //auction.Seller = User.Identity.Name;

            _repo.AddAuction(auction);

            var newAuction = _mapper.Map<AuctionDto>(auction);

            //Publish using masstransit outbox
            var newAuctionEvent = _mapper.Map<AuctionCreated>(newAuction);
            await _publishEndpoint.Publish(newAuctionEvent);

            var result = await _repo.SaveChangesAsync();

            if (!result) return BadRequest("Could not save changes to the DB");

            return CreatedAtAction(nameof(GetAuctionById),
                new { auction.Id }, newAuction);
        }

        //[Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateAuction(Guid id, UpdateAuctionDto updateAuctionDto)
        {
            var auction = await _repo.GetAuctionEntityById(id);

            if (auction == null) return NotFound();

            //if (auction.Seller != User.Identity.Name) return Forbid();

            auction.Item.Make = updateAuctionDto.Make ?? auction.Item.Make;
            auction.Item.Model = updateAuctionDto.Model ?? auction.Item.Model;
            auction.Item.Color = updateAuctionDto.Color ?? auction.Item.Color;
            auction.Item.Mileage = updateAuctionDto.Mileage ?? auction.Item.Mileage;
            auction.Item.Year = updateAuctionDto.Year ?? auction.Item.Year;

            await _publishEndpoint.Publish(_mapper.Map<AuctionUpdated>(auction));

            var result = await _repo.SaveChangesAsync();

            if (result) return Ok();

            return BadRequest("Problem saving changes");
        }

        //[Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteAuction(Guid id)
        {
            var auction = await _repo.GetAuctionEntityById(id);

            if (auction == null) return NotFound();

            //if (auction.Seller != User.Identity.Name) return Forbid();

            _repo.RemoveAuction(auction);

            await _publishEndpoint.Publish<AuctionDeleted>(new { Id = auction.Id.ToString() });

            var result = await _repo.SaveChangesAsync();

            if (!result) return BadRequest("Could not update DB");

            return Ok();
        }
    }
}
