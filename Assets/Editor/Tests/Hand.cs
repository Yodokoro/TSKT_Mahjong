﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using TSKT.Mahjongs;
using System.Linq;
#nullable enable

namespace TSKT.Tests.Mahjongs
{
    public class Hand
    {
        [Test]
        [TestCase(true, TileType.M1, TileType.M2, TileType.M3, TileType.M3)]
        [TestCase(false, TileType.M1, TileType.M2, TileType.M3, TileType.M6)]
        [TestCase(false, TileType.M2, TileType.M2, TileType.M2, TileType.M6)]
        [TestCase(true, TileType.M2, TileType.M2, TileType.M2, TileType.M2)]
        [TestCase(true, TileType.M2, TileType.M3, TileType.M4, TileType.M1)]
        [TestCase(true, TileType.M2, TileType.M3, TileType.M4, TileType.M4)]
        [TestCase(false, TileType.M2, TileType.M3, TileType.M4, TileType.M2)]
        [TestCase(false, TileType.M2, TileType.M3, TileType.M4, TileType.M3)]
        public void 喰い替え(bool expected, TileType tile1, TileType tile2, TileType tileFromOtherPlayer, TileType tileToDiscard)
        {
            var meld = new Meld((new Tile(0, tile1, false), PlayerIndex.Index0),
                (new Tile(0, tile2, false), PlayerIndex.Index0),
                (new Tile(0, tileFromOtherPlayer, false), PlayerIndex.Index1));

            Assert.AreEqual(expected, meld.Is喰い替え(new Tile(0, tileToDiscard, false)));
        }

        [Test]
        [TestCase(0, new[] {
            TileType.M1, TileType.M1,
            TileType.M3, TileType.M3,
            TileType.M5, TileType.M5,
            TileType.M8, TileType.M8,
            TileType.P1, TileType.P1,
            TileType.白, TileType.白,
            TileType.中
        })]
        [TestCase(1, new[] {
            TileType.M1, TileType.M1,
            TileType.M3, TileType.M3,
            TileType.M5, TileType.M5,
            TileType.M8, TileType.M8,
            TileType.P1, TileType.P1,
            TileType.白, TileType.白, TileType.白
        })]
        public void 七対子向聴数(int expected, TileType[] tiles)
        {
            var round = Game.Create(0, new RuleSetting()).Round;

            var hand = round.players[0].hand;
            hand.tiles.Clear();
            hand.tiles.AddRange(RandomUtil.GenerateShuffledArray(tiles.Select(_ => new Tile(0, _, red: false)).ToList()));
            Assert.IsTrue(hand.向聴数IsLessThanOrEqual(expected));
            Assert.IsFalse(hand.向聴数IsLessThanOrEqual(expected - 1));
            if (expected == 0)
            {
                Assert.AreEqual(1, hand.GetWinningTiles().Length);
            }
            else
            {
                Assert.AreEqual(0, hand.GetWinningTiles().Length);
            }
        }
    }
}