﻿#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TSKT.Mahjongs.Rounds;

namespace TSKT.Mahjongs
{
    public class AfterDiscard : IController
    {
        public Round Round => DiscardPlayer.round;
        public bool Consumed { get; private set; }
        public PlayerIndex DiscardPlayerIndex => DiscardPlayer.index;
        public Player DiscardPlayer { get; }

        Dictionary<Player, CompletedHand> PlayerRons { get; } = new Dictionary<Player, CompletedHand>();

        public Tile DiscardedTile => Round.players[(int)DiscardPlayerIndex].discardPile.Last();

        public AfterDiscard(Player discardPlayer)
        {
            Debug.Assert(discardPlayer.hand.tiles.Count % 3 == 1, "wrong hand tile count after discard");
            DiscardPlayer = discardPlayer;

            var 鳴きなし = Round.players.All(_ => _.hand.melds.Count == 0);

            foreach (var ronPlayer in Round.players)
            {
                if (ronPlayer == DiscardPlayer)
                {
                    continue;
                }
                if (ronPlayer.Furiten)
                {
                    continue;
                }
                var hand = ronPlayer.hand.Clone();
                hand.tiles.Add(DiscardedTile);
                var solution = hand.Solve();
                if (solution.向聴数 > -1)
                {
                    continue;
                }
                var 一巡目 = ronPlayer.discardedTiles.Count == 0;
                var completed = solution.ChoiceCompletedHand(ronPlayer, DiscardedTile.type,
                    ronTarget: DiscardPlayer,
                    嶺上: false,
                    海底: false,
                    河底: Round.wallTile.tiles.Count == 0,

                    天和: false,
                    地和: false,
                    人和: 鳴きなし && 一巡目,
                    槍槓: false);
                if (!completed.役無し)
                {
                    PlayerRons.Add(ronPlayer, completed);
                }
            }
        }

        static public AfterDiscard FromSerializable(in Serializables.AfterDiscard source)
        {
            var round = source.round.Deserialize();
            var player = round.players[(int)source.discardPlayerIndex];
            return new AfterDiscard(player);
        }

        public Serializables.AfterDiscard ToSerializable()
        {
            return new Serializables.AfterDiscard(this);
        }
        public Serializables.Session SerializeSession()
        {
            return new Serializables.Session(this);
        }

        bool CanRoundContinue
        {
            get
            {
                if (ShouldSuspendRound)
                {
                    return false;
                }
                return Round.wallTile.tiles.Count > 0;
            }
        }

        public void TryAttachFuriten()
        {
            foreach (var player in Round.players)
            {
                if (player == DiscardPlayer)
                {
                    continue;
                }
                player.TryAttachFuritenByOtherPlayers(DiscardedTile);
            }
        }


        bool ShouldSuspendRound
        {
            get
            {
                if (Round.game.rule.四家立直 == Rules.四家立直.流局)
                {
                    if (四家立直)
                    {
                        return true;
                    }
                }
                if (四開槓)
                {
                    return true;
                }
                if (四風子連打)
                {
                    return true;
                }
                return false;
            }
        }

        bool 四家立直 => Round.players.All(_ => _.Riichi);
        bool 四開槓
        {
            get
            {
                // 一人がカンを四回している場合は四槓子テンパイとなり流れない
                if (Round.players.Any(_ => _.hand.melds.Count(x => x.槓子) == 4))
                {
                    return false;
                }
                return Round.CountKan == 4;
            }
        }

        bool 四風子連打
        {
            get
            {
                Tile? tile = null;
                foreach (var it in Round.players)
                {
                    if (it.hand.melds.Count > 0)
                    {
                        return false;
                    }
                    if (it.discardedTiles.Count != 1)
                    {
                        return false;
                    }
                    var discardedTile = it.discardedTiles[0];
                    if (!discardedTile.type.風牌())
                    {
                        return false;
                    }
                    if (tile != null && tile.type != discardedTile.type)
                    {
                        return false;
                    }
                    tile = discardedTile;
                }
                return true;
            }
        }

        AfterDraw? AdvanceTurn(out RoundResult? roundResult)
        {
            TryAttachFuriten();

            if (CanRoundContinue)
            {
                var playerIndex = ((int)DiscardPlayerIndex + 1) % Round.players.Length;
                roundResult = null;
                return Round.players[playerIndex].Draw();
            }

            if (ShouldSuspendRound)
            {
                var result = Round.game.AdvanceRoundBy途中流局(out var gameResult);
                roundResult = new RoundResult(gameResult);
                return result;
            }

            var scoreDiffs = Round.players.ToDictionary(_ => _, _ => 0);
            var states = new Dictionary<Player, ExhausiveDrawType>();

            var 流し満貫 = Round.players
                .Where(_ => _.discardedTiles.Count == _.discardPile.Count && _.discardedTiles.All(x => x.type.么九牌()))
                .ToArray();
            if (流し満貫.Length > 0)
            {
                foreach (var it in 流し満貫)
                {
                    states.Add(it, ExhausiveDrawType.流し満貫);
                    if (it.IsDealer)
                    {
                        foreach (var player in Round.players)
                        {
                            if (player != it)
                            {
                                scoreDiffs[player] -= 4000;
                            }
                        }
                        scoreDiffs[it] += 12000;
                    }
                    else
                    {
                        foreach (var player in Round.players)
                        {
                            if (player != it)
                            {
                                scoreDiffs[player] -= player.IsDealer ? 4000 : 2000;
                            }
                        }
                        scoreDiffs[it] += 8000;
                    }
                }
            }
            else
            {
                foreach (var it in Round.players)
                {
                    states.Add(it, (it.hand.向聴数IsLessThanOrEqual(0))
                        ? ExhausiveDrawType.テンパイ
                        : ExhausiveDrawType.ノーテン);
                }
                var getterCount = states.Count(_ => _.Value == ExhausiveDrawType.テンパイ);

                if (getterCount > 0 && getterCount < 4)
                {
                    foreach (var it in states)
                    {
                        if (it.Value == ExhausiveDrawType.テンパイ)
                        {
                            scoreDiffs[it.Key] += 3000 / getterCount;
                        }
                        else
                        {
                            scoreDiffs[it.Key] -= 3000 / (4 - getterCount);
                        }
                    }
                }
            }

            foreach (var it in scoreDiffs)
            {
                it.Key.Score += it.Value;
            }

            if (states.TryGetValue(Round.Dealer, out var dealerState))
            {
                if (dealerState == ExhausiveDrawType.ノーテン)
                {
                    var result = Round.game.AdvanceRoundByノーテン流局(out var gameResult);
                    roundResult = new RoundResult(gameResult, scoreDiffs, states);
                    return result;
                }
                else if (dealerState == ExhausiveDrawType.流し満貫)
                {
                    var result = Round.game.AdvanceRoundBy親上がり(out var gameResult);
                    roundResult = new RoundResult(gameResult, scoreDiffs, states);
                    return result;
                }
                else if (dealerState == ExhausiveDrawType.テンパイ)
                {
                    var result = Round.game.AdvanceRoundByテンパイ流局(out var gameResult);
                    roundResult = new RoundResult(gameResult, scoreDiffs, states);
                    return result;
                }
                else
                {
                    throw new System.ArgumentException(dealerState.ToString());
                }
            }
            else
            {
                // 子の流し満貫
                var result = Round.game.AdvanceRoundBy子上がり(out var gameResult);
                roundResult = new RoundResult(gameResult, scoreDiffs, states);
                return result;
            }
        }

        bool CanRon(out Commands.Ron[] commands)
        {
            commands = PlayerRons.Select(_ => new Commands.Ron(_.Key, this, _.Value)).ToArray();
            return commands.Length > 0;
        }

        bool CanRon(Player player, out Commands.Ron command)
        {
            if (PlayerRons.TryGetValue(player, out var hand))
            {
                command = new Commands.Ron(player, this, hand);
                return true;
            }
            command = default;
            return false;
        }

        /// <summary>
        /// 大明槓
        /// </summary>
        bool CanOpenQuad(out Commands.Kan[] commands)
        {
            var result = new List<Commands.Kan>();
            foreach (var player in Round.players)
            {
                if (CanOpenQuad(player, out var command))
                {
                    result.Add(command);
                }
            }
            commands = result.ToArray();
            return commands.Length > 0;
        }

        /// <summary>
        /// 大明槓
        /// </summary>
        bool CanOpenQuad(Player player, out Commands.Kan command)
        {
            if (player == DiscardPlayer)
            {
                command = default;
                return false;
            }
            // 河底はカンできない
            if (Round.wallTile.tiles.Count == 0)
            {
                command = default;
                return false;
            }
            if (!player.CanOpenQuad(DiscardedTile.type))
            {
                command = default;
                return false;
            }

            command = new Commands.Kan(player, this);
            return true;
        }

        bool CanPon(out Commands.Pon[] commands)
        {
            var result = new List<Commands.Pon>();
            foreach (var player in Round.players)
            {
                if (CanPon(player, out var command))
                {
                    result.AddRange(command);
                }
            }
            commands = result.ToArray();
            return commands.Length > 0;
        }

        bool CanPon(Player player, out Commands.Pon[] commands)
        {
            if (player == DiscardPlayer)
            {
                commands = System.Array.Empty<Commands.Pon>();
                return false;
            }
            // 河底はポンできない
            if (Round.wallTile.tiles.Count == 0)
            {
                commands = System.Array.Empty<Commands.Pon>();
                return false;
            }
            if (!player.CanPon(DiscardedTile.type, out var combinations))
            {
                commands = System.Array.Empty<Commands.Pon>();
                return false;
            }
            commands = combinations.Select(_ => new Commands.Pon(player, this, _)).ToArray();
            return true;
        }

        bool CanChi(out Commands.Chi[] commands)
        {
            var result = new List<Commands.Chi>();
            foreach (var player in Round.players)
            {
                if (CanChi(player, out var command))
                {
                    result.AddRange(command);
                }
            }
            commands = result.ToArray();
            return commands.Length > 0;
        }

        bool CanChi(Player player, out Commands.Chi[] commands)
        {
            if (player == DiscardPlayer)
            {
                commands = System.Array.Empty<Commands.Chi>();
                return false;
            }

            // 河底はチーできない
            if (Round.wallTile.tiles.Count == 0)
            {
                commands = System.Array.Empty<Commands.Chi>();
                return false;
            }
            if (player.GetRelativePlayer(DiscardPlayer) != RelativePlayer.上家)
            {
                commands = System.Array.Empty<Commands.Chi>();
                return false;
            }

            if (!player.CanChi(DiscardedTile, out var combinations))
            {
                commands = System.Array.Empty<Commands.Chi>();
                return false;
            }

            commands = combinations.Select(_ => new Commands.Chi(player, this, _)).ToArray();
            return true;
        }

        public AfterDraw? DoDefaultAction(out RoundResult? roundResult)
        {
            return AdvanceTurn(out roundResult);
        }
        public ClaimingCommandSet GetExecutableClaimingCommandsBy(Player player)
        {
            Commands.Ron? ron;
            if (CanRon(player, out var _ron))
            {
                ron = _ron;
            }
            else
            {
                ron = null;
            }
            CanChi(player, out var chies);
            CanPon(player, out var pons);
            Commands.Kan? kan;
            if (CanOpenQuad(player, out var _kan))
            {
                kan = _kan;
            }
            else
            {
                kan = null;
            }

            return new ClaimingCommandSet(ron: ron, chies: chies, pons: pons, kan: kan);
        }
        public DiscardingCommandSet GetExecutableDiscardingCommandsBy(Player player)
        {
            return default;
        }

        public ICommand[] ExecutableCommands
        {
            get
            {
                var result = new List<ICommand>();

                if (CanRon(out var rons))
                {
                    result.AddRange(rons.Cast<ICommand>());
                }
                if (CanChi(out var chies))
                {
                    result.AddRange(chies.Cast<ICommand>());
                }
                if (CanPon(out var pons))
                {
                    result.AddRange(pons.Cast<ICommand>());
                }
                if (CanOpenQuad(out var kans))
                {
                    result.AddRange(kans.Cast<ICommand>());
                }

                return result.ToArray();
            }
        }
        public CommandResult ExecuteCommands(out List<ICommand> executedCommands, params ICommand[] commands)
        {
            if (Consumed)
            {
                throw new System.Exception("consumed controller");
            }
            Consumed = true;
            var selector = new CommandSelector(this);
            return selector.Execute(out executedCommands, commands);
        }
    }
}
