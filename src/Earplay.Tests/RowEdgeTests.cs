using Earplay.Core;
using Earplay.Core.Graph;
using Earplay.Core.Input;
using Earplay.Core.Screens;
using Earplay.Core.UI;
using Xunit;

namespace Earplay.Tests
{
    // Home and End in a horizontal row go to its first and last item (the board's first and last
    // column), not the stop's; in a plain list they still go to the list's ends.
    public class RowEdgeTests
    {
        private sealed class GridScreen : Screen
        {
            public override string Key => "test.grid";
            public override bool IsActive() => true;
            public override void Build(GraphBuilder b)
            {
                b.BeginStop("grid");
                for (int r = 1; r <= 3; r++)
                {
                    b.StartRow("grid");
                    for (int c = 1; c <= 3; c++)
                        b.AddItem(ControlId.Structural(c + ":" + r), Item(c + " " + r));
                    b.EndRow();
                }
                b.BeginStop("list");
                b.AddItem(ControlId.Structural("a"), Item("a"));
                b.AddItem(ControlId.Structural("b"), Item("b"));
                b.AddItem(ControlId.Structural("c"), Item("c"));
            }

            private static NodeVtable Item(string label) => new NodeVtable
            {
                Announcements = new[] { new NodeAnnouncement(() => label, kind: AnnouncementKinds.Label) },
            };
        }

        private static InputAction Action(string key) => new InputAction(key, key) { Category = InputCategory.UI };

        [Fact]
        public void HomeAndEndStayInTheRow()
        {
            Speech.Speak = (t, i) => { };
            NavInput.Current = new FakeNavInput { FrameCount = 1 };
            var nav = new GraphNavigator();
            Navigation.Active = nav;
            try
            {
                nav.Attach(new GridScreen());
                nav.EnsureFocus();
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Down)));
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Right)));
                Assert.Equal("2:2", nav.FocusedNodeId.StructuralKey);

                Assert.True(nav.OnInputJustPressed(Action(UiActions.End)));
                Assert.Equal("3:2", nav.FocusedNodeId.StructuralKey);
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Home)));
                Assert.Equal("1:2", nav.FocusedNodeId.StructuralKey);
                // Already at the first column: nothing to do there.
                Assert.False(nav.OnInputJustPressed(Action(UiActions.Home)));
                Assert.Equal("1:2", nav.FocusedNodeId.StructuralKey);
                // Up keeps the column.
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Up)));
                Assert.Equal("1:1", nav.FocusedNodeId.StructuralKey);
            }
            finally
            {
                Navigation.Active = null;
                Speech.Speak = null;
            }
        }

        [Fact]
        public void InAListHomeAndEndGoToItsEnds()
        {
            Speech.Speak = (t, i) => { };
            NavInput.Current = new FakeNavInput { FrameCount = 1 };
            var nav = new GraphNavigator();
            Navigation.Active = nav;
            try
            {
                nav.Attach(new GridScreen());
                nav.EnsureFocus();
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Next)));
                Assert.Equal("a", nav.FocusedNodeId.StructuralKey);
                Assert.True(nav.OnInputJustPressed(Action(UiActions.End)));
                Assert.Equal("c", nav.FocusedNodeId.StructuralKey);
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Home)));
                Assert.Equal("a", nav.FocusedNodeId.StructuralKey);
            }
            finally
            {
                Navigation.Active = null;
                Speech.Speak = null;
            }
        }
    }
}
