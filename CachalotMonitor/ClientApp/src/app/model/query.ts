
export class QueryMetadata {
  collectionName: string | undefined;
  propertyName: string | undefined;
  found = false;
  propertyIsCollection = false;
  propertyType: string | undefined;
  possibleValues: string[] = [];
  availableOperators: string[] = [];
  possibleValuesCount = 0;
}

export class SimpleQuery {
  propertyName: string | undefined;
  operator: string | undefined;
  values: string[] = [];
  dataType: string | undefined;
  propertyIsCollection = false;
  possibleValues: string[] = [];
  availableOperators: string[] = [];
}

export class AndQuery {
  simpleQueries: SimpleQuery[] = [];
  orderBy: string | undefined;
  descending=false;
  take = "100";
}

export class SearchRequest {
  sql: string | undefined;
  fullText: string | undefined;
}

export class SqlResponse {
  sql: string | undefined;
}

export class DataResponse {
  json: string | undefined;
  clientTimeInMilliseconds = 0;
  queryId: string | undefined;
}
