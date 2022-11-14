
export class QueryMetadata {
    collectionName: string | undefined;
    propertyName: string | undefined;
    found: boolean = false;
    propertyIsCollection: boolean = false;
    propertyType: string |undefined;
    possibleValues: string[] = [];
    availableOperators: string[] = [];
    possibleValuesCount:number = 0;
}

export class SimpleQuery{
    propertyName:string|undefined;
    operator:string|undefined;
    values:string[] = [];
    dataType:string|undefined;
    propertyIsCollection:boolean = false;
}

export class AndQuery{
    simpleQueries:SimpleQuery[] = [];
}

export class SqlResponse{
    sql:string|undefined;
}

export class DataResponse{
    json:string|undefined;
}