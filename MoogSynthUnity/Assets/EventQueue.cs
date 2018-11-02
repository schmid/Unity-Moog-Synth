// Copyright (c) 2018 Jakob Schmid
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//  
//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.
//  
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE."

using System;

// empty     queue   queue     queue      pop
// [ | | ]   [x| | ] [x|x| ]   [x|x|x]    [ |x|x]
//  B         F B     F   B     B          B F  
//  F                           F
public class EventQueue
{
    public enum EventType
    {
        None = 0,
        Note_on = 1,
        Note_off = 2,
    };

    public struct QueuedEvent
    {
        public int data;
        public Int64 time_smp;
        public EventType eventType;
        public QueuedEvent(EventType type, int data, Int64 time_smp)
        {
            this.eventType = type;
            this.data = data;
            this.time_smp = time_smp;
        }
        public void Set(EventType type, int data, Int64 time_smp)
        {
            this.eventType = type;
            this.data = data;
            this.time_smp = time_smp;
        }
    }

    /// State
    private QueuedEvent[] events;
    private int back = 0;
    private int front = 0;
    private int size = 0;
    private int capacity = -1;
    private object mutexLock = new object();

    public EventQueue(int capacity)
    {
        events = new QueuedEvent[capacity];
        this.capacity = capacity;
    }

    public bool Enqueue(EventType type, int data, Int64 time_smp)
    {
        bool didEnqueue = false;
        lock (mutexLock)
        {
            if (size < capacity)
            {
                events[back].Set(type, data, time_smp);
                back = (back + 1) % capacity;
                size++;
                didEnqueue = true;
            }
        }
        return didEnqueue;
    }
    public void Dequeue()
    {
        lock (mutexLock)
        {
            if (size > 0)
            {
                front = (front + 1) % capacity;
                --size;
            }
        }
    }
    public bool GetFront(ref QueuedEvent result)
    {
        if (size == 0)
            return false;
        result = events[front];
        return true;
    }
    public bool GetFrontAndDequeue(ref QueuedEvent result)
    {
        if (size == 0)
            return false;

        lock (mutexLock)
        {
            result = events[front];
            front = (front + 1) % capacity;
            --size;
        }
        return true;
    }
    public void Clear()
    {
        front = 0;
        back = 0;
        size = 0;
    }
    public bool IsEmpty
    {
        get { return size == 0; }
    }
    public int GetSize()
    {
        return size;
    }
}
