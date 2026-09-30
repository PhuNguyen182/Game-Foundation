using System;
using System.Linq;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core;
using NUnit.Framework;
using UnityEngine.LowLevel;
using LoopSystem = UnityEngine.LowLevel.PlayerLoopSystem;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices.Tests
{
    /// <summary>Insert / remove behaviour of <see cref="PlayerLoopUtils"/> on small hand-built loop trees.</summary>
    [TestFixture]
    public sealed class PlayerLoopUtilsTests
    {
        private struct Root { }

        private struct ParentA { }

        private struct ParentB { }

        private struct A { }

        private struct B { }

        private struct Anchor { }

        private struct Ours { }

        private static void NoOp() { }

        private static void OtherNoOp() { }

        private static LoopSystem Node(Type type, params LoopSystem[] children) =>
            new() { type = type, subSystemList = children.Length == 0 ? null : children };

        private static LoopSystem OursSystem() =>
            new() { type = typeof(Ours), updateDelegate = NoOp };

        private static string Names(LoopSystem parent) =>
            parent.subSystemList == null ? "" : string.Join(",", parent.subSystemList.Select(s => s.type.Name));

        // ------------------------------------------------------------------ InsertSystem

        [Test]
        public void InsertSystem_IntoANestedParent_PlacesItAtTheRequestedIndex()
        {
            LoopSystem loop = Node(typeof(Root),
                Node(typeof(ParentB)),
                Node(typeof(ParentA), Node(typeof(A)), Node(typeof(B))));
            LoopSystem ours = OursSystem();

            bool inserted = PlayerLoopUtils.InsertSystem<ParentA>(ref loop, in ours, 1);

            Assert.IsTrue(inserted);
            Assert.AreEqual("A,Ours,B", Names(loop.subSystemList[1]));
        }

        [Test]
        public void InsertSystem_WhenTheParentIsMissing_ReturnsFalseAndLeavesTheLoopUntouched()
        {
            LoopSystem loop = Node(typeof(Root), Node(typeof(ParentB), Node(typeof(A))));
            LoopSystem ours = OursSystem();

            bool inserted = PlayerLoopUtils.InsertSystem<ParentA>(ref loop, in ours, 0);

            Assert.IsFalse(inserted);
            Assert.AreEqual("ParentB", Names(loop));
            Assert.AreEqual("A", Names(loop.subSystemList[0]));
        }

        // ------------------------------------------------------------------ InsertSystemBefore

        [Test]
        public void InsertSystemBefore_PlacesTheSystemRightBeforeTheAnchor()
        {
            LoopSystem loop = Node(typeof(Root),
                Node(typeof(ParentA), Node(typeof(A)), Node(typeof(Anchor)), Node(typeof(B))));
            LoopSystem ours = OursSystem();

            bool inserted = PlayerLoopUtils.InsertSystemBefore<ParentA, Anchor>(ref loop, in ours);

            Assert.IsTrue(inserted);
            Assert.AreEqual("A,Ours,Anchor,B", Names(loop.subSystemList[0]));
        }

        [Test]
        public void InsertSystemBefore_WhenTheAnchorIsMissing_InsertsAtTheStart()
        {
            LoopSystem loop = Node(typeof(Root), Node(typeof(ParentA), Node(typeof(A)), Node(typeof(B))));
            LoopSystem ours = OursSystem();

            bool inserted = PlayerLoopUtils.InsertSystemBefore<ParentA, Anchor>(ref loop, in ours);

            Assert.IsTrue(inserted);
            Assert.AreEqual("Ours,A,B", Names(loop.subSystemList[0]));
        }

        [Test]
        public void InsertSystemBefore_IntoAParentWithoutChildren_CreatesTheList()
        {
            LoopSystem loop = Node(typeof(Root), Node(typeof(ParentA)));
            LoopSystem ours = OursSystem();

            bool inserted = PlayerLoopUtils.InsertSystemBefore<ParentA, Anchor>(ref loop, in ours);

            Assert.IsTrue(inserted);
            Assert.AreEqual("Ours", Names(loop.subSystemList[0]));
        }

        [Test]
        public void InsertSystemBefore_WhenTheParentIsMissing_ReturnsFalseAndLeavesTheLoopUntouched()
        {
            LoopSystem loop = Node(typeof(Root), Node(typeof(ParentB), Node(typeof(A))));
            LoopSystem ours = OursSystem();

            bool inserted = PlayerLoopUtils.InsertSystemBefore<ParentA, Anchor>(ref loop, in ours);

            Assert.IsFalse(inserted);
            Assert.AreEqual("A", Names(loop.subSystemList[0]));
        }

        // ------------------------------------------------------------------ RemoveSystem

        [Test]
        public void RemoveSystem_NestedUnderTheGivenParent_RemovesIt()
        {
            LoopSystem loop = Node(typeof(Root),
                Node(typeof(ParentB)),
                Node(typeof(ParentA), Node(typeof(A)), new LoopSystem { type = typeof(Ours), updateDelegate = NoOp },
                    Node(typeof(B))));
            LoopSystem ours = OursSystem();

            bool removed = PlayerLoopUtils.RemoveSystem<ParentA>(ref loop, in ours);

            Assert.IsTrue(removed, "The system is nested below the root but was not found.");
            Assert.AreEqual("A,B", Names(loop.subSystemList[1]));
        }

        [Test]
        public void RemoveSystem_RemovesEveryMatchingChild()
        {
            LoopSystem loop = Node(typeof(Root),
                Node(typeof(ParentA), OursSystem(), Node(typeof(A)), OursSystem()));
            LoopSystem ours = OursSystem();

            bool removed = PlayerLoopUtils.RemoveSystem<ParentA>(ref loop, in ours);

            Assert.IsTrue(removed);
            Assert.AreEqual("A", Names(loop.subSystemList[0]));
        }

        [Test]
        public void RemoveSystem_FromEveryParentOfThatType()
        {
            LoopSystem loop = Node(typeof(Root),
                Node(typeof(ParentA), OursSystem()),
                Node(typeof(ParentB), Node(typeof(ParentA), OursSystem(), Node(typeof(B)))));
            LoopSystem ours = OursSystem();

            bool removed = PlayerLoopUtils.RemoveSystem<ParentA>(ref loop, in ours);

            Assert.IsTrue(removed);
            Assert.AreEqual("", Names(loop.subSystemList[0]));
            Assert.AreEqual("B", Names(loop.subSystemList[1].subSystemList[0]));
        }

        [Test]
        public void RemoveSystem_LeavesTheSameSystemUnderOtherParentsAlone()
        {
            LoopSystem loop = Node(typeof(Root),
                Node(typeof(ParentA), Node(typeof(A))),
                Node(typeof(ParentB), OursSystem()));
            LoopSystem ours = OursSystem();

            bool removed = PlayerLoopUtils.RemoveSystem<ParentA>(ref loop, in ours);

            Assert.IsFalse(removed);
            Assert.AreEqual("Ours", Names(loop.subSystemList[1]));
        }

        [Test]
        public void RemoveSystem_SameTypeWithADifferentDelegate_IsKept()
        {
            var other = new LoopSystem { type = typeof(Ours), updateDelegate = OtherNoOp };
            LoopSystem loop = Node(typeof(Root), Node(typeof(ParentA), Node(typeof(A)), other));
            LoopSystem ours = OursSystem();

            bool removed = PlayerLoopUtils.RemoveSystem<ParentA>(ref loop, in ours);

            Assert.IsFalse(removed);
            Assert.AreEqual("A,Ours", Names(loop.subSystemList[0]));
        }

        [Test]
        public void RemoveSystem_WhenNothingMatches_ReturnsFalseAndLeavesTheLoopUntouched()
        {
            LoopSystem loop = Node(typeof(Root), Node(typeof(ParentA), Node(typeof(A)), Node(typeof(B))));
            LoopSystem ours = OursSystem();
            LoopSystem[] childrenBefore = loop.subSystemList[0].subSystemList;

            bool removed = PlayerLoopUtils.RemoveSystem<ParentA>(ref loop, in ours);

            Assert.IsFalse(removed);
            Assert.AreSame(childrenBefore, loop.subSystemList[0].subSystemList, "An unchanged list must not be rebuilt.");
        }

        [Test]
        public void RemoveSystem_FromALoopWithoutChildren_ReturnsFalse()
        {
            LoopSystem loop = Node(typeof(Root));
            LoopSystem ours = OursSystem();

            Assert.IsFalse(PlayerLoopUtils.RemoveSystem<ParentA>(ref loop, in ours));
        }
    }
}
