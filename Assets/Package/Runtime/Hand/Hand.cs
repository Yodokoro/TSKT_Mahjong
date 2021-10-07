﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
#nullable enable

namespace TSKT.Mahjongs
{
    public class Hand
    {
        readonly Player owner;
        readonly public List<Tile> tiles = new List<Tile>();
        readonly public List<Meld> melds = new List<Meld>();

        public Hand(Player owner)
        {
            this.owner = owner;
        }

        Hand(in Serializables.Hand source, Player owner)
        {
            this.owner = owner;
            melds = source.melds.Select(_ => _.Deserialzie(owner.round.wallTile)).ToList();
            tiles = source.tiles.Select(_ => owner.round.wallTile.allTiles[_]).ToList();
        }

        static public Hand FromSerializable(in Serializables.Hand source, Player owner)
        {
            return new Hand(source, owner);
        }
        public Serializables.Hand ToSerializable()
        {
            return new Serializables.Hand(this);
        }

        public void Sort()
        {
            tiles.Sort();
        }

        public Hand Clone()
        {
            var result = new Hand(owner);
            result.tiles.AddRange(tiles);
            result.melds.AddRange(melds);
            return result;
        }

        public Hands.Solution Solve()
        {
            return new Hands.Solution(this);
        }

        public bool 向聴数IsLessThanOrEqual(int value)
        {
            return Hands.Structure.向聴数IsLessThanOrEqual(this, value);
        }

        public bool 向聴数IsLessThan(int value)
        {
            return Hands.Structure.向聴数IsLessThan(this, value);
        }

        public bool 九種九牌 => tiles.Where(_ => _.type.么九牌()).Distinct().Count() >= 9;

        public IEnumerable<Tile> AllTiles
        {
            get
            {
                foreach (var it in tiles)
                {
                    yield return it;
                }
                foreach (var meld in melds)
                {
                    foreach (var (tile, from) in meld.tileFroms)
                    {
                        yield return tile;
                    }
                }
            }
        }

        public void BuildClosedQuad(TileType tileType)
        {
            var quadTiles = new (Tile, PlayerIndex)[4];
            for (int i = 0; i < 4; ++i)
            {
                var tile = tiles.First(_ => _.type == tileType);
                tiles.Remove(tile);
                quadTiles[i] = (tile, owner.index);
            }
            var meld = new Meld(quadTiles);
            melds.Add(meld);
        }

        public TileType[] GetWinningTiles()
        {
            if (!向聴数IsLessThanOrEqual(0))
            {
                return System.Array.Empty<TileType>();
            }

            // 待ち牌候補
            // 　一九字牌 : 国士無双
            // 　手牌と同じ牌 : シャンポン・単騎
            // 　手牌の隣牌 : リャンメン、ペンチャン、カンチャン

            var tilesToCheck = new List<TileType>();

            foreach (var it in tiles)
            {
                tilesToCheck.Add(it.type);

                if (it.type.IsSuited())
                {
                    var number = it.type.Number();
                    if (number > 1)
                    {
                        tilesToCheck.Add(TileTypeUtil.Get(it.type.Suit(), number - 1));
                    }
                    if (number < 9)
                    {
                        tilesToCheck.Add(TileTypeUtil.Get(it.type.Suit(), number + 1));
                    }
                }
            }
            foreach (TileType tile in System.Enum.GetValues(typeof(TileType)))
            {
                if (tile.么九牌())
                {
                    tilesToCheck.Add(tile);
                }
            }

            var allTilesInHand = AllTiles.ToArray();
            var result = new List<TileType>();
            foreach (var tile in tilesToCheck.Distinct())
            {
                // 手牌内で4枚使っている場合は待ち牌扱いにしない
                // e.g. P1が4枚あるときにP1のシャンポンや単騎にはならない
                if (allTilesInHand.Count(_ => _.type == tile) == 4)
                {
                    continue;
                }

                var clone = Clone();
                clone.tiles.Add(new Tile(0, tile, false));
                if (clone.向聴数IsLessThanOrEqual(-1))
                {
                    result.Add(tile);
                }
            }

            return result.ToArray();
        }
    }

    /// <summary>
    /// 副露
    /// </summary>
    public readonly struct Meld
    {
        public readonly (Tile tile, PlayerIndex fromPlayerIndex)[] tileFroms;

        public readonly bool 順子 => tileFroms[0].tile.type != tileFroms[1].tile.type;
        public readonly bool 槓子 => tileFroms.Length == 4;
        public readonly bool 暗槓
        {
            get
            {
                if (!槓子)
                {
                    return false;
                }
                foreach (var (_, fromPlayerIndex) in tileFroms)
                {
                    if (fromPlayerIndex != tileFroms[0].fromPlayerIndex)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public readonly Tile Min => tileFroms[0].tile;

        public Meld(params (Tile tile, PlayerIndex fromPlayerIndex)[] tileFroms)
        {
            this.tileFroms = tileFroms.OrderBy(_ => _.tile.type).ToArray();
        }
        
        Meld(in Serializables.Meld source, WallTile wallTile)
        {
            tileFroms = source.tileFroms
                .Select(_ => (wallTile.allTiles[_.tile], _.fromPlayerIndex))
                .ToArray();
        }

        static public Meld FromSerializable(in Serializables.Meld source, WallTile wallTile)
        {
            return new Meld(source, wallTile);
        }

        public readonly Serializables.Meld ToSerializable()
        {
            return new Serializables.Meld(this);
        }

        public readonly bool TryGetTileFromOtherPlayer(out (Tile tile, PlayerIndex fromPlayerIndex) result)
        {
            PlayerIndex ownerPlayerIndex;
            if (tileFroms[0].fromPlayerIndex == tileFroms[1].fromPlayerIndex)
            {
                ownerPlayerIndex = tileFroms[0].fromPlayerIndex;
            }
            else if (tileFroms[0].fromPlayerIndex == tileFroms[2].fromPlayerIndex)
            {
                ownerPlayerIndex = tileFroms[0].fromPlayerIndex;
            }
            else
            {
                ownerPlayerIndex = tileFroms[1].fromPlayerIndex;
            }

            foreach (var it in tileFroms)
            {
                if (it.fromPlayerIndex != ownerPlayerIndex)
                {
                    result = it;
                    return true;
                }
            }
            result = default;
            return false;
        }

        public readonly bool Is喰い替え(Tile tileToDiscard)
        {
            if (!TryGetTileFromOtherPlayer(out var tileFromOtherPlayer))
            {
                return false;
            }
            if (tileToDiscard.type == tileFromOtherPlayer.tile.type)
            {
                return true;
            }

            if (順子)
            {
                if (tileToDiscard.type.IsSuited())
                {
                    if (tileFroms[0].tile.type.Suit() == tileToDiscard.type.Suit())
                    {
                        var tilesNumbers = tileFroms
                            .Where(_ => _.fromPlayerIndex != tileFromOtherPlayer.fromPlayerIndex)
                            .Select(_ => _.tile.type.Number())
                            .Append(tileToDiscard.type.Number());
                        if (tilesNumbers.Sum() % 3 == 0)
                        {
                            if (tilesNumbers.Max() - tilesNumbers.Min() == 2)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
    }
}
