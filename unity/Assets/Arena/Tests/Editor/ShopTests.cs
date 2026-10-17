using NUnit.Framework;

namespace Arena.Core.Tests
{
    public class ShopTests
    {
        [Test]
        public void Potion_HealsAndIsLimited()
        {
            var p = new PlayerState(ClassId.Lamina);
            Assert.AreEqual(1, p.Potions);
            Assert.IsFalse(p.TryDrinkPotion());                 // vida cheia: não gasta
            p.Hp.Drain(80f);
            float before = p.Hp.Current;
            Assert.IsTrue(p.TryDrinkPotion());
            Assert.AreEqual(before + p.Hp.Max * PlayerState.PotionHeal, p.Hp.Current, 0.01f);
            Assert.AreEqual(0, p.Potions);
            for (int i = 0; i < 5; i++) p.AddPotion();
            Assert.AreEqual(PlayerState.MaxPotions, p.Potions);
            p.Hp.Drain(80f); p.Tick(1.1f);
            Assert.IsTrue(p.TryDrinkPotion());
            Assert.IsFalse(p.TryDrinkPotion());                 // 1 s entre goles
            p.Tick(1.1f);
            Assert.IsTrue(p.TryDrinkPotion());
        }

        [Test]
        public void Shop_SellsAt4x_BuysAt25Percent_AndRestocks()
        {
            var loot = new LootTable(7);
            var shop = new Shop();
            shop.Restock(loot);
            Assert.AreEqual(Shop.StockSize, shop.Stock.Count);
            var item = shop.Stock[0];
            Assert.AreEqual(Merchant.Price(item), Merchant.ShopPrice(item) * Merchant.BuyBackFraction, 1f);

            var p = new PlayerState(ClassId.Lamina);
            Assert.IsFalse(shop.TryBuy(item, p));               // sem ouro
            p.AddGold(Merchant.ShopPrice(item));
            Assert.IsTrue(shop.TryBuy(item, p));
            Assert.AreEqual(0, p.Gold);
            Assert.IsFalse(shop.Stock.Contains(item));
            Assert.IsFalse(shop.TryBuy(item, p));               // já vendida

            p.AddGold(1000);
            p.SetPotions(0);
            for (int i = 0; i < Shop.PotionStock; i++) Assert.IsTrue(shop.TryBuyPotion(p));
            Assert.AreEqual(0, shop.PotionsLeft);
            p.SetPotions(0);
            Assert.IsFalse(shop.TryBuyPotion(p));               // acabou o estoque
            shop.Restock(loot);
            Assert.IsTrue(shop.TryBuyPotion(p));
            Assert.AreEqual(Shop.StockSize, shop.Stock.Count);
        }

        [Test]
        public void Potions_AreSaved()
        {
            var p = new PlayerState(ClassId.Lamina);
            p.AddPotion();
            Assert.AreEqual(2, SaveData.From(p).ToPlayer().Potions);
        }
    }
}
