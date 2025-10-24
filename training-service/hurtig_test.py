#import numpy as np
#emb_matrix = np.load("C:\\Users\\mathi\\Documents\\GitHub\\TTE-Group-3\\edge_embeddings.npy")
#print(emb_matrix.shape)

from gensim.models import Word2Vec

model = Word2Vec.load("C:\\Users\\mathi\\Documents\\GitHub\\TTE-Group-3\\edge2vec_model.model")

edge = "(290573873, 290576137)"  # pick one edge from your mapping
for sim, score in model.wv.most_similar(edge, topn=5):
    print(sim, score)
