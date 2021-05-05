using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QQSConsole
{
    public class MidiHeaderCorruptedException : Exception
    {
        public MidiHeaderCorruptedException() : base()
        {

        }
        public MidiHeaderCorruptedException(string message) : base(message)
        {

        }
    }
    public class TrackHeaderCorruptedException : Exception
    {
        public TrackHeaderCorruptedException() : base()
        {

        }
        public TrackHeaderCorruptedException(string message) : base(message)
        {

        }
    }
    public class AssertionFailedException : Exception
    {
        public AssertionFailedException() : base()
        {

        }
        public AssertionFailedException(object DataExpected, object DataReceived) : base("Unexpected data: " + DataReceived.ToString() +
            ". The value expected is: " + DataExpected)
        {

        }
        public AssertionFailedException(string message) : base(message)
        {

        }
    }
    public class TrackCorruptedException : Exception
    {
        public TrackCorruptedException() : base("Corrupted Track")
        {

        }
        public TrackCorruptedException(string message) : base(message)
        {

        }
    }
    public class MidiOutputDeviceNotFoundException : Exception
    {
        public MidiOutputDeviceNotFoundException() : base("Midi output device not found.")
        {

        }
        public MidiOutputDeviceNotFoundException(string message) : base(message)
        {

        }
    }
    public class MidiOutputDeviceNotInitializedException : Exception
    {
        public MidiOutputDeviceNotInitializedException() : base("Midi output device not initialized.")
        {

        }
        public MidiOutputDeviceNotInitializedException(string message) : base(message)
        {

        }
    }
}
