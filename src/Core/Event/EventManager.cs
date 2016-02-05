/*
 *  This file is part of uEssentials project.
 *      https://uessentials.github.io/
 *
 *  Copyright (C) 2015-2016  Leonardosc
 *
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License along
 *  with this program; if not, write to the Free Software Foundation, Inc.,
 *  51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Essentials.Api.Event;
using Essentials.Common;

namespace Essentials.Core.Event
{
    public class EventManager : IEventManager
    {
        private Dictionary<EventHolder, List<Delegate>> HandlerMap { get; }

        internal EventManager()
        {
            HandlerMap = new Dictionary<EventHolder, List<Delegate>>();
        }

        public void RegisterAll( Type type )
        {
            foreach ( var listenerMethod in type.GetMethods( (BindingFlags) 60 ) )
            {
                var eventHandlerAttrs = listenerMethod.GetCustomAttributes( typeof (SubscribeEvent), false );
                if ( eventHandlerAttrs.Length < 1 ) continue;

                var eventHandlerAttr = (SubscribeEvent) eventHandlerAttrs[0];
                var eventTarget = eventHandlerAttr.TargetType;
                var targetFieldName = eventHandlerAttr.TargetFieldName;

                lock ( HandlerMap )
                {
                    var holder = GetHolder( eventTarget, targetFieldName );

                    EventInfo eventInfo;
                    List<Delegate> methodDelegates;

                    if ( holder == null )
                    {
                        var eventTargetType = (Type) (eventTarget is Type ?
                            eventTarget : eventTarget.GetType());

                        eventInfo = eventTargetType.GetEvent( targetFieldName );

                        holder = new EventHolder()
                        {
                            EventInfo = eventInfo,
                            Target = eventTarget
                        };

                        methodDelegates = new List<Delegate>();
                    }
                    else
                    {
                        eventTarget = holder.Target;
                        eventInfo = holder.EventInfo;

                        methodDelegates = HandlerMap[holder];
                    }

                    var methodDelegate = Delegate.CreateDelegate(
                        eventInfo.EventHandlerType,
                        Activator.CreateInstance( type ),
                        listenerMethod
                     );

                    eventInfo.AddEventHandler( eventTarget, methodDelegate );

                    methodDelegates.Add( methodDelegate );
                    HandlerMap[holder] = methodDelegates;
                }
            }
        }

        public void RegisterAll<TEventType>()
        {
            RegisterAll( typeof (TEventType) );
        }

        public void RegisterAll( Assembly asm )
        {
            asm.GetTypes().ForEach( RegisterAll );
        }

        public void RegisterAll( string targetNamespace )
        {
            GetType().Assembly.GetTypes()
                .Where( t => t.Namespace.EqualsIgnoreCase( targetNamespace ) )
                .ForEach( RegisterAll );
        }

        public void UnregisterAll<TEventType>()
        {
            UnregisterAll( typeof(TEventType) );
        }

        public void UnregisterAll( Type type )
        {
            lock ( HandlerMap )
            {
                var unregisteredDelegates = new List<Delegate>();
                var unregisteredHolders = new List<EventHolder>();

                for ( var j = 0; j < HandlerMap.ToList().Count; j++ )
                {
                    var handler = HandlerMap.ToList()[j];

                    foreach ( var delegateMethod in handler.Value )
                    {
                        if ( delegateMethod.Method.ReflectedType != type) continue;

                        handler.Key.EventInfo.RemoveEventHandler( handler.Key.Target, delegateMethod );
                        unregisteredDelegates.Add( delegateMethod );
                    }

                    handler.Value.RemoveAll( @delegate =>
                        unregisteredDelegates.Contains( @delegate ) );

                    if ( handler.Value.Count == 0 ) unregisteredHolders.Add( handler.Key );
                }

                // Remove empty EventHolders
                foreach ( var holder in unregisteredHolders )
                {
                    HandlerMap.Remove( holder );
                }
            }
        }

        public void UnregisterAll( Assembly asm )
        {
            asm.GetTypes().ForEach( UnregisterAll );
        }

        public void UnregisterAll( string targetNamespace )
        {
            GetType().Assembly.GetTypes()
                .Where( t => t.Namespace.EqualsIgnoreCase( targetNamespace ) )
                .ForEach( RegisterAll );
        }

        private EventHolder GetHolder( object target, string fieldName )
        {
            lock ( HandlerMap )
            {
                return HandlerMap.Keys.FirstOrDefault( holder => holder.Target.Equals( target ) && 
                    holder.EventInfo.Name.Equals( fieldName ) );
            }
        }

        /// <summary>
        /// Simple class that store the Instance of EventInfo of an event, 
        /// and the instance or type of EventInfo "owner"
        /// </summary>
        public sealed class EventHolder
        {
            public object Target;
            public EventInfo EventInfo;
        }
    }
}