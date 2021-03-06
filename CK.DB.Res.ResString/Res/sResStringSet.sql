﻿-- SetupConfig: {}
--
-- Sets a string value for a resource. 
-- When Value is null, it is removed.
--
create procedure CK.sResStringSet
(
	@ResId int,
	@Value nvarchar(400)
)
as
begin
	set nocount on;
	if @ResId <= 0 throw 50000, 'Res.InvalidResId', 1;
	merge CK.tResString as target
		using( select ResId = @ResId ) 
		as source on source.ResId = target.ResId
		when matched and @Value is null then delete
		when matched then update set Value = @Value
		when not matched by target and @Value is not null then insert( ResId, Value ) values( source.ResId, @Value );
	return 0;
end