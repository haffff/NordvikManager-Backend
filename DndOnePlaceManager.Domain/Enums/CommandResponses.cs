using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Domain.Enums
{
    public enum CommandResponse
    {
        /// <summary>
        /// Command has been handles succesfully
        /// </summary>
        Ok, 
        /// <summary>
        /// User lacks persmissions
        /// </summary>
        NoPermission,
        /// <summary>
        /// Wrong aruments were provided
        /// </summary>
        WrongArguments,
        /// <summary>
        /// Resource was not found
        /// </summary>
        NoResource,
        /// <summary>
        /// entity already exists
        /// </summary>
        AlreadyExists,
        /// <summary>
        /// no changes were made
        /// </summary>
        NoChange,
    }
}
