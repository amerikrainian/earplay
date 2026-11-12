using System.Collections.Generic;
using Earplay.Core.Screens;
using Earplay.Core.Strings;
using Earplay.Core.UI;

namespace Earplay.Module.Round
{
    /// <summary>
    /// The board seen from the shop: the shop's Peek (<c>ShopManager.TogglePeek</c>) fades the shop's
    /// canvas so the board shows, the game's control zone staying the shop's. While it peeks, the
    /// round's board, word and status read here in read-only form (nothing acts: the round is not on,
    /// and the keyboard stays the mod's); Escape toggles the peek off, back to the shop.
    /// </summary>
    public sealed class PeekScreen : CompositeScreen
    {
        public PeekScreen()
        {
            Add(new BoardSection(readOnly: true));
            Add(new WordSection(readOnly: true));
            Add(new StatusSection(readOnly: true));
        }

        public override string Key => "game.peek";
        public override bool AllowsTypeahead => false;

        public override bool IsActive()
        {
            var shop = ShopManager.Instance;
            return shop != null && shop.shopIsShowing && shop.peeking;
        }

        protected override IEnumerable<ElementAction> OwnActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.shop.unpeek"), _ =>
            {
                if (ShopManager.Instance != null && ShopManager.Instance.peeking) ShopManager.Instance.TogglePeek();
            });
        }
    }
}
