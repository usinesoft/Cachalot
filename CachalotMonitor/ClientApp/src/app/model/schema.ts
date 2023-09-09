export interface Schema {
  serverSide: ServerSide[];
  collectionName: string;
  storageLayout: string;
  fullText: string[];
}

export interface ServerSide {
  name: string;
  indexType: string;
  jsonName: string;
  order: number;
  isCollection: boolean;
  confirmMode: boolean;
}

export interface SchemaUpdateRequest {
  collectionName: string;
  propertyName: string;
  indexType: string;
}
