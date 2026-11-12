using System.Collections.Generic;
using Earplay.Core;
using Earplay.Core.Graph;
using Earplay.Core.Input;
using Earplay.Core.Screens;
using Earplay.Core.UI;
using Xunit;

namespace Earplay.Tests
{
    // Tab and Shift+Tab always wrap round the stops; with one stop they do nothing at all.
    public class TabWrapTests
    {
        private sealed class StopsScreen : Screen
        {
            private readonly string[] _stops;
            public StopsScreen(params string[] stops) { _stops = stops; }
            public override string Key => "test.stops";
            public override bool IsActive() => true;
            public override void Build(GraphBuilder b)
            {
                foreach (var stop in _stops)
                {
                    b.BeginStop(stop);
                    b.AddItem(ControlId.Structural(stop + ".a"), Item(stop + " a"));
                    b.AddItem(ControlId.Structural(stop + ".b"), Item(stop + " b"));
                }
            }

            private static NodeVtable Item(string label) => new NodeVtable
            {
                Announcements = new[] { new NodeAnnouncement(() => label, kind: AnnouncementKinds.Label) },
            };
        }

        private static InputAction Action(string key) => new InputAction(key, key) { Category = InputCategory.UI };

        private static GraphNavigator Attach(Screen screen, List<string> spoken)
        {
            Speech.Speak = (t, i) => spoken.Add(t);
            NavInput.Current = new FakeNavInput { FrameCount = 1 };
            var nav = new GraphNavigator();
            Navigation.Active = nav;
            nav.Attach(screen);
            nav.EnsureFocus();
            spoken.Clear();
            return nav;
        }

        [Fact]
        public void TabPastTheLastStopWrapsToTheFirstAndShiftTabBack()
        {
            var spoken = new List<string>();
            try
            {
                var nav = Attach(new StopsScreen("one", "two", "three"), spoken);
                Assert.Equal("one.a", nav.FocusedNodeId.StructuralKey);

                Assert.True(nav.OnInputJustPressed(Action(UiActions.Prev)));
                Assert.Equal("three.a", nav.FocusedNodeId.StructuralKey);

                Assert.True(nav.OnInputJustPressed(Action(UiActions.Next)));
                Assert.Equal("one.a", nav.FocusedNodeId.StructuralKey);
            }
            finally
            {
                Navigation.Active = null;
                Speech.Speak = null;
            }
        }

        [Fact]
        public void WithOneStopTabIsConsumedAndSaysNothing()
        {
            var spoken = new List<string>();
            try
            {
                var nav = Attach(new StopsScreen("only"), spoken);
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Down)));
                Assert.Equal("only.b", nav.FocusedNodeId.StructuralKey);
                nav.EnsureFocus();
                spoken.Clear();

                Assert.True(nav.OnInputJustPressed(Action(UiActions.Next)));
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Prev)));
                nav.EnsureFocus();
                Assert.Equal("only.b", nav.FocusedNodeId.StructuralKey);
                Assert.Empty(spoken);
            }
            finally
            {
                Navigation.Active = null;
                Speech.Speak = null;
            }
        }
    }
}
