// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

package com.microsoft.signalr;

import java.lang.reflect.Type;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.locks.ReentrantLock;

class CallbackMap {
    private final Map<String, List<InvocationHandler>> handlers = new HashMap<>();
    private final ReentrantLock lock = new ReentrantLock();

    public InvocationHandler put(String target, ActionBase action, Type... types) {
        try {
            lock.lock();
            InvocationHandler handler = new InvocationHandler(action, types);
            if (!handlers.containsKey(target)) {
                handlers.put(target, new ArrayList<>());
            }
            handlers.get(target).add(handler);
            return handler;
        } finally {
            lock.unlock();
        }
    }

    public List<InvocationHandler> get(String key) {
        try {
            lock.lock();
            return handlers.get(key);
        } finally {
            lock.unlock();
        }
    }

    public void remove(String key) {
        try {
            lock.lock();
            handlers.remove(key);
        } finally {
            lock.unlock();
        }
    }
}
