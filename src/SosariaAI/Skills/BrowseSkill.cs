using System;
using Server;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// Walks to a shop in reach and looks round it for a few minutes: strolls the floor, turns
/// to the vendor, says a word now and then, and moves on. Half the time the browser first looks
/// over the player vendors in reach, and when one has a piece it wants at a price it would pay
/// (<see cref="PlayerVendorMall"/>), it walks there and buys it. Nothing is bought at an NPC
/// shop; a real want there is the shopping errand's work.
/// </summary>
public sealed class BrowseSkill : Skill
{
    private static readonly ILogger logger = SosariaLog.For(typeof(BrowseSkill));

    private readonly LoiterStay _stay = new();
    private SosariaCharacter _character;
    private TravelSkill _walk;
    private VendorPiece? _piece;
    private DateTime _until;
    private DateTime _nextLine;

    public override string Name => BrowseRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _walk = null;
        _piece = null;

        if (!People.InWorld(character))
        {
            return CannotStart(NotInWorldReason);
        }

        var roll = Utility.Random(int.MaxValue);

        if (BeginVendorVisit(character, roll))
        {
            return true;
        }

        for (var attempt = 0; attempt < BrowseRules.ShopRoles.Length; attempt++)
        {
            var shop = ShopFinder.Nearest(character, BrowseRules.TokenAt(roll, attempt), Point3D.Zero);

            if (shop == Point3D.Zero)
            {
                continue;
            }

            _walk = new TravelSkill(shop, NavLimits.ShopArrivalRange, arrivalFloor: true);

            if (_walk.Begin(character))
            {
                return true;
            }
        }

        _walk = null;
        return CannotStart("no shop in reach");
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted || !People.InWorld(_character))
        {
            return Fail(LeftWorldReason);
        }

        if (_walk != null)
        {
            return ArriveWhenThere();
        }

        if (_stay.Tick(out _) == SkillStatus.Failed)
        {
            return Fail("the walk on the shop floor failed");
        }

        if (Core.Now >= _nextLine)
        {
            _nextLine = Core.Now + BrowseRules.LineGap(Utility.Random(int.MaxValue));
            FaceVendor();
            Talk.Maybe(_character, TalkCategory.ShopBrowse, BrowseRules.LinePercent);
        }

        return Core.Now >= _until ? SkillStatus.Done : SkillStatus.Running;
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _stay.Abort();
    }

    public override void Resume(TimeSpan held)
    {
        _until = SkillClock.Shift(_until, held);
        _nextLine = SkillClock.Shift(_nextLine, held);
        _walk?.Resume(held);
        _stay.Resume(held);
    }

    private SkillStatus ArriveWhenThere()
    {
        var walk = _walk.Tick();

        if (walk == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _walk = null;

        if (walk == SkillStatus.Failed)
        {
            return Fail("the walk to the shop failed");
        }

        if (_piece is { } piece)
        {
            BuyAtVendor(piece);
        }

        if (!_stay.Begin(_character, _character.Location, BrowseRules.FloorRadius, BrowseRules.StayWeight))
        {
            return Fail("no room on the shop floor");
        }

        var roll = Utility.Random(int.MaxValue);
        _until = Core.Now + BrowseRules.Length(roll);
        _nextLine = Core.Now + BrowseRules.LineGap(roll);
        return SkillStatus.Running;
    }

    // A player vendor in reach with a piece the browser wants is the first stop.
    private bool BeginVendorVisit(SosariaCharacter character, int roll)
    {
        if (!PlayerVendorMall.LooksAtVendors(roll) ||
            PlayerVendorMall.PieceFor(character, PlayerVendorMall.ShopReach, Core.Now) is not { } piece)
        {
            return false;
        }

        _walk = new TravelSkill(piece.Vendor.Location, PlayerVendorMall.BuyRange, arrivalFloor: true);

        if (_walk.Begin(character))
        {
            _piece = piece;
            return true;
        }

        _walk = null;
        return false;
    }

    private void BuyAtVendor(VendorPiece piece)
    {
        if (!PlayerVendorRules.Buy(_character, piece.Vendor, piece.Piece, Core.Now))
        {
            return;
        }

        _character.NoteMusingEvent(MusingRules.Bought(Appraisal.NounOf(piece.Piece), piece.Price));

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} bought {Piece} off {Owner}'s vendor for {Gold} gold at {Location}",
                _character.Name,
                Appraisal.NounOf(piece.Piece),
                piece.Vendor.Owner?.Name ?? "a player",
                piece.Price,
                _character.Location
            );
        }
    }

    private void FaceVendor()
    {
        if (_piece is { Vendor: { Deleted: false } playerVendor })
        {
            _character.Direction = _character.GetDirectionTo(playerVendor);
            return;
        }

        var vendors = VendorDeal.VendorsNear(_character, VendorDeal.CounterRange);

        if (vendors.Count > 0)
        {
            _character.Direction = _character.GetDirectionTo(vendors[0]);
        }
    }
}
