using System;
using System.Collections.Generic;
using System.Text;

namespace Transmitter;

public class DeserializationException(string msg) : Exception(msg) { }
