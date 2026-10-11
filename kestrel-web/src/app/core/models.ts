export interface ChatTurn {
  role: string;
  content: string;
}

export interface ChatRequest {
  message: string;
  history?: ChatTurn[];
  equipmentId?: string | null;
  conversationId?: string;
}

export interface Citation {
  index: number;
  documentId?: string;
  title: string;
  sourcePath?: string;
  /** UI alias for sourcePath */
  source?: string;
  section?: string;
  excerpt?: string;
  equipmentId?: string;
  docType?: string;
  url?: string;
}

export interface Equipment {
  id: string;
  name: string;
  model?: string;
  type?: string;
  plantId?: string;
  area?: string;
  /** Derived label for filters */
  location?: string;
}

export interface FeedbackRequest {
  conversationId: string;
  messageId: string;
  helpful: boolean;
  comment?: string;
  traceId?: string;
}

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  citations?: Citation[];
  conversationId?: string;
}

export interface ToolEvent {
  name: string;
  status?: string;
  detail?: string;
  timestamp: number;
}

export interface ModelInfo {
  chatProfile: string;
  embeddingProfile: string;
  chatModel: string;
  embeddingModel: string;
  supportsTools: boolean;
  collection: string;
}

export interface SourceDocument {
  id: string;
  title: string;
  docType: string;
  sourcePath: string;
  equipmentId?: string;
  plantId?: string;
  content: string;
}
