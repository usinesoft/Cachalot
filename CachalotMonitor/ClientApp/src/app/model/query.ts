
export class QueryMetadata {
    collectionName: string | undefined;
    propertyName: string | undefined;
    found: boolean = false;
    propertyIsCollection: boolean = false;
    propertyType: string |undefined;
    possibleValues: string[] = [];
    availableOperators: string[] = [];
}

export class SimpleQuery{
    propertyName:string|undefined;
    operator:string|undefined;
    values:string[] = [];
}

export class AndQuery{
    simpleQueries:SimpleQuery[] = [new SimpleQuery];
}