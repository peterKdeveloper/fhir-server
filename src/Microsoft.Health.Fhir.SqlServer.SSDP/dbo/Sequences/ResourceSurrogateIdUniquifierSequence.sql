﻿CREATE SEQUENCE dbo.ResourceSurrogateIdUniquifierSequence
        AS int
        START WITH 0
        INCREMENT BY 1
        MINVALUE 0
        MAXVALUE 79999
        CYCLE
        CACHE 1000000