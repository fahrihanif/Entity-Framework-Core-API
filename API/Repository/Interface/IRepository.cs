﻿using System.Collections.Generic;

namespace API.Repository.Interface
{

    //This method get called by GeneralRepository to implement Generic Repository Pattern
    public interface IRepository<Entity, Key> where Entity : class
    {
        IEnumerable<Entity> Get();
        Entity Get(Key key);
        int Insert(Entity entity);
        int Update(Entity entity);
        int Delete(Key key);
    }
}
