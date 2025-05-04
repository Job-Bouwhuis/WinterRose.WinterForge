namespace WinterRose.WinterForgeSerializing.Workers
{
    /// <summary>
    /// Operation codes that the deserialization process for WinterForge understands
    /// </summary>
    public enum OpCode
    {
        /// <summary>
        /// Defines an object. <br></br><br></br>
        /// 
        /// EG "0 System.Numerics.Vector3 0 0" <br></br>
        /// 0 - define <br></br>
        /// type name <br></br>
        /// object ID of 0 <br></br>
        /// 0 items from the stack for constructor arguments
        /// </summary>
        DEFINE = 0,
        /// <summary>
        /// Sets the value of a field on the current object <br></br><br></br>
        /// 
        /// EG "1 X 25"<br></br>
        /// 1 - SET<br></br>
        /// X - field name<br></br>
        /// 25 - value
        /// </summary>
        SET = 1,
        /// <summary>
        /// Ends the working on the most recent defined object.<br></br><br></br>
        /// 
        /// EG "2 0"<br></br>
        /// 2 - END<br></br>
        /// 0 - object key its ending. this must match the most recent defined object that has not yet had this end operation called on it.
        /// </summary>
        END = 2,
        /// <summary>
        /// pushes a value to the stack. used for constructor and method calls<br></br><br></br>
        /// 
        /// EG "3 5"<br></br>
        /// 3 - PUSH<br></br>
        /// 5 - value. value can also be a _ref() or _stack() call
        /// </summary>
        PUSH = 3,
        /// <summary>
        /// Calls a method. this opcode has not yet been fully designed. therefor an example can not yet be given
        /// </summary>
        CALL = 4,
        /// <summary>
        /// Adds an element to the current list<br></br><br></br>
        /// 
        /// EG "5 _ref(4)"<br></br>
        /// 5 - ELEMENT<br></br>
        /// _ref(4) - value, the reference of an object with ID 4
        /// </summary>
        ELEMENT = 5,
        /// <summary>
        /// begins creating a new list<br></br><br></br>
        /// 
        /// EG "6 System.Numerics.Vector3"<br></br>
        /// 6 - LIST_START<br></br>
        /// list item type
        /// </summary>
        LIST_START = 6,
        /// <summary>
        /// Ends a list and puts it to the stack. can then be accessed by <see cref="SET"/> using '_stack()'<br></br><br></br>
        /// 
        /// Has no arguments. line should be only "7".
        /// </summary>
        LIST_END = 7,
        /// <summary>
        /// tells the <see cref="InstructionExecutor"/> to return the object of which key is given<br></br><br></br>
        /// 
        /// EG "8 4"<br></br>
        /// 8 - RETURN<br></br>
        /// 4 - key of which object reference to return
        /// </summary>
        RET = 8,
        /// <summary>
        /// invokes <see cref="InstructionExecutor.ProgressMark"/>
        /// </summary>
        PROGRESS = 9,
        /// <summary>
        /// Attempts to access the top stack value. on success puts the value on the stack. on fail, exception<br></br><br></br>
        /// 
        /// EG: "10 Player"<br></br>
        /// </summary>
        ACCESS = 10,
        /// <summary>
        /// Sets the value on the top stack value, rather than the current working object<br></br><br></br>
        /// 
        /// refer to <see cref="SET"/> for the example
        /// </summary>
        SETACCESS = 11,
        /// <summary>
        /// Takes the top stack item and puts it as a reference item on the given ID 
        /// (can be used to override an id reference)<br></br><br></br>
        /// 
        /// EG: 12 0<br></br>
        /// 12 - AS<br></br>
        /// 0 - store the stack item on ID 0. even if an object at that ID already exists
        /// </summary>
        AS = 12
    }

}
