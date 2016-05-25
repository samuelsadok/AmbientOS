using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS
{
    /*
    string Name { get; }
    IAOSObject Parent { get; }


    public class AOSObjectLock : IDisposable
    {
        private AOSObject parent;
        public AOSObjectLock(AOSObject parent, bool exclusive)
        {
            parent.Lock(exclusive);
        }

        public void Dispose()
        {
            lock (this) {
                if (parent == null)
                    throw new ObjectDisposedException("the lock was already released");
                parent.Unlock(this);
                parent = null;
            }
        }
    }


    object lockRef = new object();
    List<AOSObjectLock> locks; // we track the locks so we can warn or kill the lockers if the object is about to disappear
    bool locked = false;
    bool exclusive = false;

    private void Lock(AOSObjectLock objLock, bool exclusive)
    {
        lock (lockRef) {
            if (locked && (exclusive || this.exclusive))
                throw new AOSLockException(exclusive, this); // todo: give more information about the culprit(s)
            locked = true;
            this.exclusive = exclusive;

            locks.Add(objLock);
        }
    }

    private void Unlock(AOSObjectLock objLock)
    {
        lock (lockRef) {
            locks.Remove(objLock);
            locked = false;
        }
    }

    public AOSObjectLock Lock(bool exclusive)
    {
        return new AOSObjectLock(this, exclusive);
    }


    /// <summary>
    /// Shall return the value of the attribute with the specified name.
    /// If this fails (unavailable, unauthorized, not implemented, ...), it's in most cases appropriate to return null.
    /// </summary>
    public abstract string GetAttribute(string name);

    /// <summary>
    /// Same as GetAttribute, but catches any exception (in which case null is returned).
    /// </summary>
    public string TryGetAttribute(string name)
    {
        try {
            return GetAttribute(name);
        } catch (Exception) {
            return null;
        }
    }

    public AOSService Service { get; private set; }

    protected AOSObject(AOSService publisher)
    {
        Service = publisher;
    }
*/
}
