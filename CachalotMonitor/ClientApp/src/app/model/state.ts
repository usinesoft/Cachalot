import { AndQuery } from "./query";

export class DataScreenState{
    collectionName:string|undefined;
    visibleColumns:string[] = [];
    currentQuery:AndQuery|undefined;
    
}

export class SchemaScreenState{
    collectionName:string|undefined;    
}