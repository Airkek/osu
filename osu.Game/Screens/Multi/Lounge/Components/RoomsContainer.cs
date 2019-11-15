// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.Multiplayer;
using osuTK;

namespace osu.Game.Screens.Multi.Lounge.Components
{
    public class RoomsContainer : CompositeDrawable
    {
        public Action<Room> JoinRequested;

        private readonly IBindableList<Room> rooms = new BindableList<Room>();

        private readonly FillFlowContainer<DrawableRoom> roomFlow;
        public IReadOnlyList<DrawableRoom> Rooms => roomFlow;

        [Resolved(CanBeNull = true)]
        private Bindable<FilterCriteria> filter { get; set; }

        [Resolved]
        private Bindable<Room> currentRoom { get; set; }

        [Resolved]
        private IRoomManager roomManager { get; set; }

        public RoomsContainer()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = roomFlow = new FillFlowContainer<DrawableRoom>
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(2),
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            rooms.BindTo(roomManager.Rooms);

            rooms.ItemsAdded += addRooms;
            rooms.ItemsRemoved += removeRooms;

            roomManager.RoomsUpdated += updateSorting;

            addRooms(rooms);
        }

        protected override void LoadComplete()
        {
            filter?.BindValueChanged(f => scheduleFilter());

            if (filter != null)
                Filter(filter.Value);
        }

        public void Filter(FilterCriteria criteria)
        {
            roomFlow.Children.ForEach(r =>
            {
                if (criteria == null)
                    r.MatchingFilter = true;
                else
                {
                    bool matchingFilter = true;
                    matchingFilter &= r.FilterTerms.Any(term => term.IndexOf(criteria.SearchString, StringComparison.InvariantCultureIgnoreCase) >= 0);

                    switch (criteria.SecondaryFilter)
                    {
                        default:
                        case SecondaryFilter.Public:
                            matchingFilter &= r.Room.Availability.Value == RoomAvailability.Public;
                            break;
                    }

                    r.MatchingFilter = matchingFilter;
                }
            });
        }

        private ScheduledDelegate scheduledFilter;

        private void scheduleFilter()
        {
            if (filter == null)
                return;

            scheduledFilter?.Cancel();
            scheduledFilter = Scheduler.AddDelayed(() => Filter(filter.Value), 200);
        }

        private void addRooms(IEnumerable<Room> rooms)
        {
            foreach (var r in rooms)
                roomFlow.Add(new DrawableRoom(r) { Action = () => selectRoom(r) });

            filter?.TriggerChange();
        }

        private void removeRooms(IEnumerable<Room> rooms)
        {
            foreach (var r in rooms)
            {
                var toRemove = roomFlow.Single(d => d.Room == r);
                toRemove.Action = null;

                roomFlow.Remove(toRemove);

                selectRoom(null);
            }
        }

        private void updateSorting()
        {
            foreach (var room in roomFlow)
                roomFlow.SetLayoutPosition(room, room.Room.Position.Value);
        }

        private void selectRoom(Room room)
        {
            var drawable = roomFlow.FirstOrDefault(r => r.Room == room);

            if (drawable != null && drawable.State == SelectionState.Selected)
                JoinRequested?.Invoke(room);
            else
                roomFlow.Children.ForEach(r => r.State = r.Room == room ? SelectionState.Selected : SelectionState.NotSelected);

            currentRoom.Value = room;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (roomManager != null)
                roomManager.RoomsUpdated -= updateSorting;
        }
    }
}
