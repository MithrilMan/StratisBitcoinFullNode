using System.Collections.Generic;

namespace Stratis.Features.Wallet.Repository
{
    public interface IRepositoryBase
    {
    }

    /// <summary>
    /// HD Account repository interface to fetch and store data based on use cases.
    /// </summary>
    /// <typeparam name="TPrimaryKey">The type of the primary key.</typeparam>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    public interface IRepositoryBase<TPrimaryKey, TEntity> : IRepositoryBase
    {
        /// <summary>
        /// Gets the <typeparamref name="TEntity" /> entity by its identifier.
        /// </summary>
        /// <param name="id">The entity identifier.</param>
        /// <returns>
        /// The <see cref="TEntity" /> represented by its id
        /// </returns>
        TEntity GetById(TPrimaryKey id);

        /// <summary>
        /// Gets all the available <typeparamref name="TEntity" /> entities.
        /// </summary>
        /// <returns>
        /// The list of available <see cref="TEntity" /> entities.
        /// </returns>
        IEnumerable<TEntity> GetAll();

        /// <summary>
        /// Adds the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>
        /// The added <see cref="TEntity" />
        /// </returns>
        TEntity Add(TEntity entity);

        /// <summary>
        /// Updates the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>
        /// The updated <see cref="TEntity" />
        /// </returns>
        TEntity Update(TEntity entity);

        /// <summary>
        /// Deletes the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>
        /// The removed <see cref="TEntity" />
        /// </returns>
        TEntity Delete(TEntity entity);
    }
}
