﻿using CK.Core;
using CK.DB.Actor;
using CK.SqlServer;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.DB.Acl.AclType.Tests
{
    [TestFixture]
    public class AclTypeTests
    {
        [Test]
        public async Task creating_and_destroying_type()
        {
            var map = TestHelper.StObjMap;
            var aclType = map.Default.Obtain<AclTypeTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int id = await aclType.CreateAclTypeAsync( ctx, 1 );
                aclType.Database.AssertScalarEquals( 2, "select count(*) from CK.tAclTypeGrantLevel where AclTypeId = @0", id );
                aclType.Database.AssertEmptyReader( "select * from CK.tAclTypeGrantLevel where AclTypeId = @0 and GrantLevel not in (0, 127)", id );
                await aclType.DestroyAclTypeAsync( ctx, 1, id );
                aclType.Database.AssertEmptyReader( "select * from CK.tAclTypeGrantLevel where AclTypeId = @0", id );
            }
        }

        [Test]
        public async Task constrained_levels_must_not_be_deny_and_0_and_127_can_not_be_removed()
        {
            var map = TestHelper.StObjMap;
            var aclType = map.Default.Obtain<AclTypeTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int id = await aclType.CreateAclTypeAsync( ctx, 1 );
                await aclType.SetGrantLevelAsync( ctx, 1, id, 87, true );
                aclType.Database.AssertScalarEquals( 3, "select count(*) from CK.tAclTypeGrantLevel where AclTypeId = @0", id );
                await aclType.SetGrantLevelAsync( ctx, 1, id, 88, true );
                aclType.Database.AssertScalarEquals( 4, "select count(*) from CK.tAclTypeGrantLevel where AclTypeId = @0", id );

                // Removing an unexisting level is always possible...
                await aclType.SetGrantLevelAsync( ctx, 1, id, 126, false );
                await aclType.SetGrantLevelAsync( ctx, 1, id, 1, false );
                aclType.Database.AssertScalarEquals( 4, "select count(*) from CK.tAclTypeGrantLevel where AclTypeId = @0", id );
                // ...except if it is 0 or 127.
                Assert.Throws<SqlException>( async () => await aclType.SetGrantLevelAsync( ctx, 1, id, 0, false ) );
                Assert.Throws<SqlException>( async () => await aclType.SetGrantLevelAsync( ctx, 1, id, 127, false ) );

                // Configured GrantLevel must not be deny level:
                Assert.Throws<SqlException>( async () => await aclType.SetGrantLevelAsync( ctx, 1, id, 128, true ) );
                Assert.Throws<SqlException>( async () => await aclType.SetGrantLevelAsync( ctx, 1, id, 255, true ) );
                Assert.Throws<SqlException>( async () => await aclType.SetGrantLevelAsync( ctx, 1, id, 255, false ) );

                await aclType.SetGrantLevelAsync( ctx, 1, id, 87, false );
                aclType.Database.AssertScalarEquals( 3, "select count(*) from CK.tAclTypeGrantLevel where AclTypeId = @0", id );
                await aclType.SetGrantLevelAsync( ctx, 1, id, 88, false );
                aclType.Database.AssertScalarEquals( 2, "select count(*) from CK.tAclTypeGrantLevel where AclTypeId = @0", id );

                await aclType.DestroyAclTypeAsync( ctx, 1, id );
            }
        }

        [Test]
        public async Task type_can_not_be_destroyed_when_typed_acl_exist()
        {
            var map = TestHelper.StObjMap;
            var acl = map.Default.Obtain<AclTable>();
            var aclType = map.Default.Obtain<AclTypeTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int idType = await aclType.CreateAclTypeAsync( ctx, 1 );
                int idAcl = await aclType.CreateAclAsync( ctx, 1, idType );
                aclType.Database.AssertScalarEquals( idType, "select AclTypeId from CK.tAcl where AclId = @0", idAcl );
                Assert.Throws<SqlException>( async () => await aclType.DestroyAclTypeAsync( ctx, 1, idType ) );
                acl.DestroyAcl( ctx, 1, idAcl );
                await aclType.DestroyAclTypeAsync( ctx, 1, idType );
            }
        }

        [Test]
        public void typed_acl_with_constrained_levels_control_their_grant_levels()
        {
            var map = TestHelper.StObjMap;
            var user = map.Default.Obtain<UserTable>();
            var acl = map.Default.Obtain<AclTable>();
            var aclType = map.Default.Obtain<AclTypeTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int idType = aclType.CreateAclType( ctx, 1 );
                int idAcl = aclType.CreateAcl( ctx, 1, idType );
                int idUser = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );

                // Sets the type as a Constrained one.
                aclType.SetConstrainedGrantLevel( ctx, 1, idType, true );
                // Allowing GrantLevel: 50
                Assert.Throws<SqlDetailedException>( () => acl.AclGrantSet( ctx, 1, idAcl, idUser, "A reason", 50 ) );
                Assert.Throws<SqlDetailedException>( () => acl.AclGrantSet( ctx, 1, idAcl, idUser, "A reason", 255 - 50 ) );
                aclType.SetGrantLevel( ctx, 1, idType, 50, true );
                acl.AclGrantSet( ctx, 1, idAcl, idUser, "A reason", 50 );
                acl.AclGrantSet( ctx, 1, idAcl, idUser, "A reason", 255 - 50 );

                // Allowing GrantLevel: 75
                Assert.Throws<SqlDetailedException>( () => acl.AclGrantSet( ctx, 1, idAcl, idUser, null, 75 ) );
                Assert.Throws<SqlDetailedException>( () => acl.AclGrantSet( ctx, 1, idAcl, idUser, null, 255 - 75 ) );
                aclType.SetGrantLevel( ctx, 1, idType, 75, true );
                acl.AclGrantSet( ctx, 1, idAcl, idUser, null, 255 - 75 );
                acl.AclGrantSet( ctx, 1, idAcl, idUser, null, 75 );

                // Since 75 and 50 are currently used, one can not remove it.
                Assert.Throws<SqlDetailedException>( () => aclType.SetGrantLevel( ctx, 1, idType, 75, false ) );
                Assert.Throws<SqlDetailedException>( () => aclType.SetGrantLevel( ctx, 1, idType, 50, false ) );

                // Removing the 50 configuration: we can now remove the level 50.
                acl.AclGrantSet( ctx, 1, idAcl, idUser, "A reason", 0 );
                aclType.SetGrantLevel( ctx, 1, idType, 50, false );
                // We can no more use the level 50.
                Assert.Throws<SqlDetailedException>( () => acl.AclGrantSet( ctx, 1, idAcl, idUser, "Won't do it!", 50 ) );

                // Cleaning the Acl and the type.
                user.DestroyUser( ctx, 1, idUser );
                acl.DestroyAcl( ctx, 1, idAcl );
                aclType.DestroyAclType( ctx, 1, idType );
            }
        }

        [Test]
        public void existing_level_prevents_set_constrained()
        {
            var map = TestHelper.StObjMap;
            var aclType = map.Default.Obtain<AclTypeTable>();
            var acl = map.Default.Obtain<AclTable>();
            var user = map.Default.Obtain<UserTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int idType = aclType.CreateAclType( ctx, 1 );
                int idAcl = aclType.CreateAcl( ctx, 1, idType );
                int idUser = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );

                acl.AclGrantSet( ctx, 1, idAcl, idUser, "A reason", 50 );
                // Sets the type as a Constrained one.
                Assert.Throws<SqlDetailedException>( () => aclType.SetConstrainedGrantLevel( ctx, 1, idType, true ) );

                acl.AclGrantSet( ctx, 1, idAcl, idUser, "A reason", 0 );
                aclType.SetConstrainedGrantLevel( ctx, 1, idType, true );

                // Cleaning the Acl and the type.
                user.DestroyUser( ctx, 1, idUser );
                acl.DestroyAcl( ctx, 1, idAcl );
                aclType.DestroyAclType( ctx, 1, idType );
            }
        }
    }

}