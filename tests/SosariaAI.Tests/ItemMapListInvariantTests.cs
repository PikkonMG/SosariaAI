using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// An item is on a sector list exactly when it lies on the ground of a real map.
/// Random engine moves must never leave an item with a parent on a sector list.
/// </summary>
public class ItemMapListInvariantTests : IDisposable
{
    private const uint FirstSerial = 0x7100;
    private const int ContainerCount = 6;
    private const int LooseCount = 10;
    private const int Steps = 20000;
    private const int Seed = 1999;
    private const int OperationCount = 7;

    // The items end on the shared test land, where a later class would find them.
    private readonly List<Item> _pool = [];

    public void Dispose() => TestMap.Remove(_pool);

    [Fact]
    public void RandomEngineMoves_KeepSectorListFlagInStepWithParent()
    {
        var land = TestMap.EnsureLand();
        TestMap.EnsureRunningWorld();
        var rng = new Random(Seed);
        var nextSerial = FirstSerial;

        for (var i = 0; i < ContainerCount; i++)
        {
            _pool.Add(new Bag((Serial)nextSerial++));
        }

        for (var i = 0; i < LooseCount; i++)
        {
            _pool.Add(new Item((Serial)nextSerial++));
        }

        var history = new List<string>();

        Item Fresh(int slot) => slot < ContainerCount ? new Bag((Serial)nextSerial++) : new Item((Serial)nextSerial++);

        for (var step = 0; step < Steps; step++)
        {
            var slot = rng.Next(_pool.Count);

            if (_pool[slot].Deleted)
            {
                // Deleted with its parent bag in an earlier step.
                _pool[slot] = Fresh(slot);
            }

            var item = _pool[slot];
            var operation = rng.Next(OperationCount);
            var point = new Point3D(rng.Next(land.Width), rng.Next(land.Height), 0);
            history.Add($"{step}: op {operation} on 0x{item.Serial.Value:X} parent={item.Parent != null} map={item.Map}");

            switch (operation)
            {
                case 0:
                    {
                        if (_pool[rng.Next(ContainerCount)] is Container { Deleted: false } target && target != item)
                        {
                            target.AddItem(item);
                        }

                        break;
                    }
                case 1:
                    {
                        if (_pool[rng.Next(ContainerCount)] is Container { Deleted: false } target && target != item)
                        {
                            // DropItem needs client art data, so do its two steps by hand.
                            target.AddItem(item);
                            item.Location = point;
                        }

                        break;
                    }
                case 2:
                    {
                        item.MoveToWorld(point, land);
                        break;
                    }
                case 3:
                    {
                        item.Internalize();
                        break;
                    }
                case 4:
                    {
                        item.Map = rng.Next(2) == 0 ? land : Map.Internal;
                        break;
                    }
                case 5:
                    {
                        item.Location = point;
                        break;
                    }
                default:
                    {
                        item.Delete();
                        _pool[slot] = Fresh(slot);
                        break;
                    }
            }

            foreach (var checkedItem in _pool)
            {
                var onGround = checkedItem.Parent == null && !checkedItem.Deleted &&
                               checkedItem.Map != null && checkedItem.Map != Map.Internal;

                Assert.True(
                    checkedItem.OnLinkList == onGround,
                    $"0x{checkedItem.Serial.Value:X} OnLinkList={checkedItem.OnLinkList} onGround={onGround}\n" +
                    string.Join('\n', history.GetRange(Math.Max(0, history.Count - 6), Math.Min(6, history.Count)))
                );
            }
        }
    }
}
